package main

import (
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"regexp"
	"runtime"
	"strings"
	"time"

	"github.com/manifoldco/promptui"
	"github.com/spf13/cobra"
)

var (
	Version = "dev"
	BaseDir = os.Getenv("PICOBLOG_BASE_DIR")
	rootCmd = &cobra.Command{
		Use:   "picoblog",
		Short: "A CLI tool for managing blog posts",
	}
)

type BlogPost struct {
	Title  string
	Date   time.Time
	Public bool
	Draft  bool
}

func createMarkdown(file string, post BlogPost) error {
	publicText := "true"
	if !post.Public {
		publicText = "false"
	}
	draftText := "true"
	if !post.Draft {
		draftText = "false"
	}
	date := post.Date.Format("2006-01-02")

	content := fmt.Sprintf(`---
title: %s
date: %s
cover:
weather:
public: %s
draft: %s
---`, post.Title, date, publicText, draftText)

	return os.WriteFile(file, []byte(content), 0644)
}

func init() {
	newCmd := &cobra.Command{
		Use:   "new [title]",
		Short: "Create a new blog post",
		Args:  cobra.ExactArgs(1),
		RunE:  newPost,
	}

	newCmd.Flags().StringP("date", "d", "", "Post date (YYYY-MM-DD)")
	newCmd.Flags().BoolP("public", "p", true, "Make post public")
	newCmd.Flags().BoolP("draft", "r", false, "Mark as draft")

	openCmd := &cobra.Command{
		Use:   "open [date]",
		Short: "Open blog post by date",
		Args:  cobra.ExactArgs(1),
		RunE:  openPost,
	}

	rootCmd.AddCommand(newCmd, openCmd)
}

func newPost(cmd *cobra.Command, args []string) error {
	title := args[0]
	dateStr, _ := cmd.Flags().GetString("date")
	public, _ := cmd.Flags().GetBool("public")
	draft, _ := cmd.Flags().GetBool("draft")

	var date time.Time
	var subPath string

	if dateStr == "" {
		date = time.Now()
	} else {
		dateStr = strings.NewReplacer("/", "-", ".", "-").Replace(dateStr)
		if len(dateStr) == 7 {
			subPath = strings.ReplaceAll(dateStr, "-", "/")
		} else {
			var err error
			date, err = parseDate(dateStr)
			if err != nil {
				return err
			}
		}
	}

	if subPath == "" {
		subPath = date.Format("2006/01/02")
	}

	path := filepath.Join(BaseDir, subPath)
	re := regexp.MustCompile(`\s+`)
	filename := re.ReplaceAllString(strings.TrimSpace(title), "-")
	file := filepath.Join(path, filename+".md")

	if _, err := os.Stat(file); err == nil {
		return openPost(cmd, []string{date.Format("2006-01-02")})
	}

	if err := os.MkdirAll(path, 0755); err != nil {
		return err
	}

	post := BlogPost{
		Title:  title,
		Date:   date,
		Public: public,
		Draft:  draft,
	}

	if err := createMarkdown(file, post); err != nil {
		return err
	}

	if err := touch(file, date); err != nil {
		return err
	}

	fmt.Printf("🌟 opening %s 🌟\n", file)
	if err := openFile(file); err != nil {
		fmt.Printf("Error opening file %s: %v\n", file, err)
	}
	return openFile(path)
}

func parseDate(input string) (time.Time, error) {
	formats := []string{
		"2006-01-02",
		"Jan. 02, 2006",
	}

	for _, format := range formats {
		if t, err := time.Parse(format, input); err == nil {
			return t.Add(12 * time.Hour), nil
		}
	}
	return time.Time{}, fmt.Errorf("no valid date format found for: %s", input)
}

func openPost(cmd *cobra.Command, args []string) error {
	date, err := parseDate(args[0])
	if err != nil {
		return err
	}

	subPath := date.Format("2006/01/02")
	path := filepath.Join(BaseDir, subPath)

	files, err := getFiles(path)
	if err != nil {
		return err
	}

	// If only one file exists, open it directly
	if len(files) == 1 {
		file := filepath.Join(path, files[0])
		fmt.Printf("Opening %s\n", file)
		if err := openFile(file); err != nil {
			return err
		}
		return openFile(path)
	}

	// Configure the prompt
	prompt := promptui.Select{
		Label: fmt.Sprintf("Select markdown file for %s", args[0]),
		Items: files,
		Size:  10, // Show 10 items at a time
		Templates: &promptui.SelectTemplates{
			Label:    "{{ . | cyan }}",
			Active:   "→ {{ . | cyan }}",
			Inactive: "  {{ . | white }}",
			Selected: "✓ {{ . | green }}",
		},
		Keys: &promptui.SelectKeys{
			Prev: promptui.Key{Code: promptui.KeyPrev, Display: "↑/k"},
			Next: promptui.Key{Code: promptui.KeyNext, Display: "↓/j"},
		},
	}

	// Show the selection prompt
	index, _, err := prompt.Run()
	if err != nil {
		if err == promptui.ErrInterrupt || err == promptui.ErrAbort {
			fmt.Println("Operation cancelled")
			return nil
		}
		return err
	}

	// Open selected file
	selectedFile := filepath.Join(path, files[index])
	fmt.Printf("Opening %s\n", selectedFile)
	if err := openFile(selectedFile); err != nil {
		return err
	}
	return openFile(path)
}

func touch(fname string, times time.Time) error {
	if _, err := os.Stat(fname); err != nil {
		file, err := os.Create(fname)
		if err != nil {
			return err
		}
		file.Close()
	}
	return os.Chtimes(fname, times, times)
}

func getFiles(path string) ([]string, error) {
	var files []string
	entries, err := os.ReadDir(path)
	if err != nil {
		return nil, err
	}

	for _, entry := range entries {
		if !entry.IsDir() && strings.HasSuffix(entry.Name(), ".md") {
			files = append(files, entry.Name())
		}
	}
	return files, nil
}

func openFile(path string) error {
	var cmd string
	switch os := runtime.GOOS; os {
	case "darwin":
		cmd = "open"
	case "linux":
		cmd = "xdg-open"
	case "windows":
		cmd = "cmd /c start"
	default:
		return fmt.Errorf("unsupported operating system")
	}

	command := exec.Command(cmd, path)
	return command.Run()
}

func main() {
	if err := rootCmd.Execute(); err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
}
