package main

import (
	"fmt"
	"log"
	"os"
	"os/exec"
	"path/filepath"
	"regexp"
	"runtime"
	"strings"
	"time"

	"github.com/urfave/cli/v2"
)

var baseDir string

func init() {
	baseDir = os.Getenv("PICOBLOG_BASE_DIR")
	if baseDir == "" {
		log.Fatal("PICOBLOG_BASE_DIR environment variable not set")
	}
}

// Rest of the helper functions remain the same...
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

func touchFile(fname string, timestamp time.Time) error {
	_, err := os.Stat(fname)
	if err != nil {
		if os.IsNotExist(err) {
			file, err := os.Create(fname)
			if err != nil {
				return err
			}
			file.Close()
		} else {
			return err
		}
	}

	return os.Chtimes(fname, timestamp, timestamp)
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

func parseDate(dateStr string) (time.Time, error) {
	layouts := []string{
		"2006-01-02",
		"Jan. 02, 2006",
		"January 02, 2006",
		"2006/01/02",
		"2006.01.02",
	}

	for _, layout := range layouts {
		if t, err := time.Parse(layout, dateStr); err == nil {
			return t.Add(12 * time.Hour), nil
		}
	}

	return time.Time{}, fmt.Errorf("no valid date format found for: %s", dateStr)
}

func openFile(path string) error {
	files, err := getFiles(path)
	if err != nil {
		return err
	}

	if len(files) == 1 {
		file := filepath.Join(path, files[0])
		if err := openWithDefaultApp(file); err != nil {
			return fmt.Errorf("error opening file %s: %v", file, err)
		}
		return nil
	}

	for _, file := range files {
		fmt.Println(file)
	}
	return nil
}

func main() {
	app := &cli.App{
		Name:  "picoblog",
		Usage: "A simple blog post manager",
		Commands: []*cli.Command{
			{
				Name:  "new",
				Usage: "Create a new blog post",
				Flags: []cli.Flag{
					&cli.StringFlag{
						Name:    "date",
						Aliases: []string{"d"},
						Usage:   "Post date (YYYY-MM-DD)",
					},
					&cli.BoolFlag{
						Name:    "public",
						Value:   true,
						Usage:   "Make post public",
					},
					&cli.BoolFlag{
						Name:    "draft",
						Value:   false,
						Usage:   "Mark as draft",
					},
				},
				Action: func(c *cli.Context) error {
					title := strings.Join(c.Args().Slice(), " ") // Join all arguments as the title
					if title == "" {
						return fmt.Errorf("title argument required")
					}

					var date time.Time
					var err error

					dateStr := c.String("date")
					if dateStr != "" {
						date, err = parseDate(dateStr)
						if err != nil {
							return err
						}
					} else {
						date = time.Now()
					}

					subPath := date.Format("2006/01/02")
					path := filepath.Join(baseDir, subPath)

					re := regexp.MustCompile(`\s+`)
					filename := re.ReplaceAllString(strings.TrimSpace(title), "-")
					file := filepath.Join(path, filename+".md")

					if _, err := os.Stat(file); err == nil {
						return openFile(path)
					}

					if err := os.MkdirAll(path, 0755); err != nil {
						return err
					}

					if err := createMarkdown(file, title, date, c.Bool("public"), c.Bool("draft")); err != nil {
						return err
					}

					if err := touchFile(file, date); err != nil {
						return err
					}

					fmt.Printf("🌟 opening %s 🌟\n", file)
					if err := openWithDefaultApp(file); err != nil {
						fmt.Printf("Error opening file: %v\n", err)
					}
					if err := openWithDefaultApp(path); err != nil {
						fmt.Printf("Error opening path: %v\n", err)
					}

					return nil
				},
			},
			{
				Name:  "open",
				Usage: "Open an existing blog post",
				Action: func(c *cli.Context) error {
					if c.NArg() < 1 {
						return fmt.Errorf("date argument required")
					}

					date, err := parseDate(c.Args().Get(0))
					if err != nil {
						return err
					}

					subPath := date.Format("2006/01/02")
					path := filepath.Join(baseDir, subPath)

					return openFile(path)
				},
			},
		},
	}

	if err := app.Run(os.Args); err != nil {
		log.Fatal(err)
	}
}