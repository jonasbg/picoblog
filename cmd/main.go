package main

import (
	"fmt"
	"log"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"time"

	"github.com/spf13/cobra"
)

var (
	baseDir string
	dateStr string
	public  bool
	draft   bool
)

func init() {
	baseDir = os.Getenv("PICOBLOG_BASE_DIR")
	if baseDir == "" {
		log.Fatal("PICOBLOG_BASE_DIR environment variable not set")
	}

	newCmd.Flags().StringVarP(&dateStr, "date", "d", "", "Post date (YYYY-MM-DD)")
	newCmd.Flags().BoolVar(&public, "public", true, "Set post as public")
	newCmd.Flags().BoolVar(&draft, "draft", false, "Set post as draft")

	openCmd.Flags().StringVarP(&dateStr, "date", "d", "", "Post date (YYYY-MM-DD)")

	rootCmd.AddCommand(newCmd, openCmd)
}

func openWithDefaultApp(path string) error {
	var cmd *exec.Cmd
	switch runtime.GOOS {
	case "darwin":
		cmd = exec.Command("open", path)
	case "windows":
		cmd = exec.Command("cmd", "/c", "start", path)
	case "linux":
		cmd = exec.Command("xdg-open", path)
	default:
		return fmt.Errorf("unsupported operating system: %s", runtime.GOOS)
	}

	cmd.Stderr = os.Stderr
	return cmd.Run()
}

func createMarkdown(file string, title string, date time.Time, public bool, draft bool) error {
	publicText := "true"
	if !public {
		publicText = "false"
	}
	draftText := "true"
	if !draft {
		draftText = "false"
	}

	content := fmt.Sprintf(`---
title: %s
date: %s
cover:
weather:
public: %s
draft: %s
---`, title, date.Format("2006-01-02"), publicText, draftText)

	return os.WriteFile(file, []byte(content), 0644)
}

func createPost(title string, date time.Time, public, draft bool) error {
	dirPath := filepath.Join(baseDir, date.Format("2006/01/02"))
	if err := os.MkdirAll(dirPath, 0755); err != nil {
		return fmt.Errorf("failed to create directories: %w", err)
	}

	filePath := filepath.Join(dirPath, title+".md")

	if _, err := os.Stat(filePath); err == nil {
		return fmt.Errorf("post already exists: %s", filePath)
	}

	if err := createMarkdown(filePath, title, date, public, draft); err != nil {
		return fmt.Errorf("failed to create markdown file: %w", err)
	}

	return openWithDefaultApp(filePath)
}

func findPost(title string, date time.Time) (string, error) {
	dirPath := filepath.Join(baseDir, date.Format("2006/01/02"))
	filePath := filepath.Join(dirPath, title+".md")

	if _, err := os.Stat(filePath); err != nil {
		return "", fmt.Errorf("post not found: %s", filePath)
	}

	return filePath, nil
}

func getDate() (time.Time, error) {
	if dateStr == "" {
		return time.Now(), nil
	}
	return time.Parse("2006-01-02", dateStr)
}

var rootCmd = &cobra.Command{
	Use:   "picoblog",
	Short: "A simple blog post manager",
}

var newCmd = &cobra.Command{
	Use:   "new [title]",
	Short: "Create a new blog post",
	Args:  cobra.ExactArgs(1),
	RunE: func(cmd *cobra.Command, args []string) error {
		title := args[0]

		date, err := getDate()
		if err != nil {
			return fmt.Errorf("invalid date format: %w", err)
		}

		return createPost(title, date, public, draft)
	},
}

var openCmd = &cobra.Command{
	Use:   "open [title]",
	Short: "Open an existing blog post",
	Args:  cobra.ExactArgs(1),
	RunE: func(cmd *cobra.Command, args []string) error {
		title := args[0]

		date, err := getDate()
		if err != nil {
			return fmt.Errorf("invalid date format: %w", err)
		}

		filePath, err := findPost(title, date)
		if err != nil {
			return err
		}

		return openWithDefaultApp(filePath)
	},
}

func main() {
	if err := rootCmd.Execute(); err != nil {
		log.Fatal(err)
	}
}