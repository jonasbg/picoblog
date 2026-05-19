package main

import (
	"bufio"
	"encoding/json"
	"fmt"
	"io"
	"os"
	"os/exec"
	"path/filepath"
	"regexp"
	"runtime"
	"sort"
	"strings"
	"sync/atomic"
	"time"

	"github.com/manifoldco/promptui"
	"github.com/spf13/cobra"
)

var (
	Version = "dev"
	BaseDir = os.Getenv("PICOBLOG_BASE_DIR")
	rootCmd = &cobra.Command{
		Use:           "picoblog",
		Short:         "A CLI tool for managing blog posts",
		Long:          rootHelp(),
		Version:       Version,
		SilenceUsage:  true,
		SilenceErrors: true,
	}
)

type BlogPost struct {
	Title  string
	Date   time.Time
	Public bool
	Draft  bool
}

type PostIndex struct {
	BaseDir     string        `json:"base_dir"`
	GeneratedAt time.Time     `json:"generated_at"`
	Posts       []IndexedPost `json:"posts"`
}

type IndexedPost struct {
	Title    string    `json:"title"`
	Date     string    `json:"date"`
	Path     string    `json:"path"`
	RelPath  string    `json:"rel_path"`
	Public   *bool     `json:"public,omitempty"`
	Draft    *bool     `json:"draft,omitempty"`
	ModTime  time.Time `json:"mod_time"`
	Size     int64     `json:"size"`
	SortTime time.Time `json:"sort_time"`
}

type postDisplay struct {
	Label string
	Post  IndexedPost
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

func rootHelp() string {
	cachePath, err := cacheFile()
	if err != nil {
		cachePath = filepath.Join("~", ".config", "picoblog", "posts.json")
	}

	return fmt.Sprintf(`A CLI tool for managing blog posts.

Configuration:
  PICOBLOG_BASE_DIR   Folder that contains your year/month/day markdown posts.
                      Example: export PICOBLOG_BASE_DIR="$HOME/blog"

Cache:
  Post metadata is cached at %s.
  Run "picoblog cache refresh" after large manual edits, or pass --refresh to list/find.
`, cachePath)
}

func requireBaseDir() error {
	if strings.TrimSpace(BaseDir) == "" {
		return fmt.Errorf("PICOBLOG_BASE_DIR is not set; point it at your blog folder, for example: export PICOBLOG_BASE_DIR=\"$HOME/blog\"")
	}

	info, err := os.Stat(BaseDir)
	if err != nil {
		return fmt.Errorf("PICOBLOG_BASE_DIR %q is not readable: %w", BaseDir, err)
	}
	if !info.IsDir() {
		return fmt.Errorf("PICOBLOG_BASE_DIR %q is not a directory", BaseDir)
	}
	return nil
}

func configDir() (string, error) {
	if configHome := os.Getenv("XDG_CONFIG_HOME"); strings.TrimSpace(configHome) != "" {
		return filepath.Join(configHome, "picoblog"), nil
	}
	userConfigDir, err := os.UserConfigDir()
	if err != nil {
		return "", err
	}
	return filepath.Join(userConfigDir, "picoblog"), nil
}

func cacheFile() (string, error) {
	dir, err := configDir()
	if err != nil {
		return "", err
	}
	return filepath.Join(dir, "posts.json"), nil
}

func refreshCache(showProgress bool) (PostIndex, error) {
	if err := requireBaseDir(); err != nil {
		return PostIndex{}, err
	}

	var posts []IndexedPost
	var postCount atomic.Int64
	stopProgress := startCacheProgress(showProgress, &postCount)
	defer stopProgress()

	if err := filepath.WalkDir(BaseDir, func(path string, entry os.DirEntry, err error) error {
		if err != nil {
			return err
		}
		if entry.IsDir() {
			if shouldSkipDir(entry.Name()) && path != BaseDir {
				return filepath.SkipDir
			}
			return nil
		}
		if !strings.EqualFold(filepath.Ext(entry.Name()), ".md") {
			return nil
		}

		post, err := readIndexedPost(path)
		if err != nil {
			return err
		}
		posts = append(posts, post)
		postCount.Add(1)
		return nil
	}); err != nil {
		return PostIndex{}, err
	}

	sortPosts(posts)
	index := PostIndex{
		BaseDir:     BaseDir,
		GeneratedAt: time.Now(),
		Posts:       posts,
	}

	cachePath, err := cacheFile()
	if err != nil {
		return PostIndex{}, err
	}
	if err := os.MkdirAll(filepath.Dir(cachePath), 0755); err != nil {
		return PostIndex{}, err
	}

	data, err := json.MarshalIndent(index, "", "  ")
	if err != nil {
		return PostIndex{}, err
	}
	if err := os.WriteFile(cachePath, data, 0644); err != nil {
		return PostIndex{}, err
	}

	return index, nil
}

func startCacheProgress(show bool, count *atomic.Int64) func() {
	if !show {
		return func() {}
	}

	done := make(chan struct{})
	finished := make(chan struct{})
	go func() {
		defer close(finished)
		frames := []string{"◐", "◓", "◑", "◒"}
		frame := 0
		ticker := time.NewTicker(120 * time.Millisecond)
		defer ticker.Stop()

		fmt.Fprintf(os.Stderr, "%s Building post cache...", frames[frame])
		for {
			select {
			case <-ticker.C:
				frame = (frame + 1) % len(frames)
				fmt.Fprintf(os.Stderr, "\r%s Building post cache... %d posts found", frames[frame], count.Load())
			case <-done:
				fmt.Fprintf(os.Stderr, "\r✓ Building post cache... %d posts found\n", count.Load())
				return
			}
		}
	}()

	return func() {
		close(done)
		<-finished
	}
}

func shouldSkipDir(name string) bool {
	switch strings.ToLower(strings.TrimSpace(name)) {
	case "", ".", "eadir", "@eadir", ".git", ".svn", ".hg", "node_modules", "bin", "obj", "dist", "build", ".cache", ".config":
		return true
	}
	return strings.HasPrefix(name, ".")
}

func loadCache(refresh bool) (PostIndex, error) {
	if refresh {
		return refreshCache(true)
	}
	if err := requireBaseDir(); err != nil {
		return PostIndex{}, err
	}

	cachePath, err := cacheFile()
	if err != nil {
		return PostIndex{}, err
	}
	data, err := os.ReadFile(cachePath)
	if err != nil {
		return refreshCache(true)
	}

	var index PostIndex
	if err := json.Unmarshal(data, &index); err != nil {
		return refreshCache(true)
	}
	if index.BaseDir != BaseDir {
		return refreshCache(true)
	}

	sortPosts(index.Posts)
	return index, nil
}

func readIndexedPost(path string) (IndexedPost, error) {
	info, err := os.Stat(path)
	if err != nil {
		return IndexedPost{}, err
	}

	relPath, err := filepath.Rel(BaseDir, path)
	if err != nil {
		relPath = path
	}

	post := IndexedPost{
		Title:    strings.TrimSuffix(filepath.Base(path), filepath.Ext(path)),
		Path:     path,
		RelPath:  relPath,
		ModTime:  info.ModTime(),
		Size:     info.Size(),
		SortTime: info.ModTime(),
	}

	file, err := os.Open(path)
	if err != nil {
		return post, nil
	}
	defer file.Close()

	metadata := readFrontMatter(file)
	if title := metadata["title"]; title != "" {
		post.Title = title
	}
	if date := metadata["date"]; date != "" {
		post.Date = date
		if parsed, err := parseDate(date); err == nil {
			post.SortTime = parsed
		}
	} else if date := dateFromPath(relPath); date != "" {
		post.Date = date
		if parsed, err := parseDate(date); err == nil {
			post.SortTime = parsed
		}
	}
	if public, ok := parseBool(metadata["public"]); ok {
		post.Public = &public
	}
	if draft, ok := parseBool(metadata["draft"]); ok {
		post.Draft = &draft
	}

	return post, nil
}

func readFrontMatter(reader io.Reader) map[string]string {
	scanner := bufio.NewScanner(reader)
	metadata := map[string]string{}
	if !scanner.Scan() || strings.TrimSpace(scanner.Text()) != "---" {
		return metadata
	}

	for scanner.Scan() {
		line := strings.TrimSpace(scanner.Text())
		if line == "---" {
			break
		}
		key, value, ok := strings.Cut(line, ":")
		if !ok {
			continue
		}
		metadata[strings.ToLower(strings.TrimSpace(key))] = strings.Trim(strings.TrimSpace(value), `"'`)
	}
	return metadata
}

func parseBool(value string) (bool, bool) {
	switch strings.ToLower(strings.TrimSpace(value)) {
	case "true", "yes", "1":
		return true, true
	case "false", "no", "0":
		return false, true
	default:
		return false, false
	}
}

func dateFromPath(relPath string) string {
	parts := strings.Split(filepath.ToSlash(relPath), "/")
	if len(parts) < 3 {
		return ""
	}
	candidate := strings.Join(parts[:3], "-")
	if _, err := parseDate(candidate); err != nil {
		return ""
	}
	return candidate
}

func sortPosts(posts []IndexedPost) {
	sort.SliceStable(posts, func(i, j int) bool {
		if posts[i].SortTime.Equal(posts[j].SortTime) {
			return posts[i].RelPath < posts[j].RelPath
		}
		return posts[i].SortTime.After(posts[j].SortTime)
	})
}

func formatPost(post IndexedPost) string {
	date := post.Date
	if date == "" {
		date = post.ModTime.Format("2006-01-02")
	}

	var markers []string
	if post.Draft != nil && *post.Draft {
		markers = append(markers, "draft")
	}
	if post.Public != nil && !*post.Public {
		markers = append(markers, "private")
	}
	suffix := ""
	if len(markers) > 0 {
		suffix = " [" + strings.Join(markers, ",") + "]"
	}

	return fmt.Sprintf("%s  %s  %s%s", date, post.Title, post.RelPath, suffix)
}

func postDisplays(posts []IndexedPost, query string) []postDisplay {
	query = strings.ToLower(strings.TrimSpace(query))
	displays := make([]postDisplay, 0, len(posts))
	for _, post := range posts {
		label := formatPost(post)
		if query != "" && !strings.Contains(strings.ToLower(label), query) {
			continue
		}
		displays = append(displays, postDisplay{Label: label, Post: post})
	}
	return displays
}

func selectPost(posts []IndexedPost, query string) (IndexedPost, bool, error) {
	displays := postDisplays(posts, query)
	if len(displays) == 0 {
		return IndexedPost{}, false, fmt.Errorf("no posts found")
	}

	if selected, ok, err := selectPostWithFZF(displays, query); err == nil || ok {
		return selected, ok, err
	}

	items := make([]string, len(displays))
	for i, display := range displays {
		items[i] = display.Label
	}

	prompt := promptui.Select{
		Label: "Select post",
		Items: items,
		Size:  15,
		Templates: &promptui.SelectTemplates{
			Label:    "{{ . | cyan }}",
			Active:   "> {{ . | cyan }}",
			Inactive: "  {{ . | white }}",
			Selected: "{{ . | green }}",
		},
	}
	index, _, err := prompt.Run()
	if err != nil {
		if err == promptui.ErrInterrupt || err == promptui.ErrAbort {
			fmt.Println("Operation cancelled")
			return IndexedPost{}, false, nil
		}
		return IndexedPost{}, false, err
	}

	return displays[index].Post, true, nil
}

func selectPostWithFZF(displays []postDisplay, query string) (IndexedPost, bool, error) {
	if _, err := exec.LookPath("fzf"); err != nil {
		return IndexedPost{}, false, err
	}

	command := exec.Command("fzf", "--prompt=picoblog> ", "--height=40%", "--reverse")
	if strings.TrimSpace(query) != "" {
		command.Args = append(command.Args, "--query", query)
	}

	stdin, err := command.StdinPipe()
	if err != nil {
		return IndexedPost{}, false, err
	}
	go func() {
		defer stdin.Close()
		for _, display := range displays {
			fmt.Fprintln(stdin, display.Label)
		}
	}()

	output, err := command.Output()
	if err != nil {
		if exitErr, ok := err.(*exec.ExitError); ok && exitErr.ExitCode() == 130 {
			fmt.Println("Operation cancelled")
			return IndexedPost{}, false, nil
		}
		return IndexedPost{}, false, err
	}

	selected := strings.TrimSpace(string(output))
	for _, display := range displays {
		if display.Label == selected {
			return display.Post, true, nil
		}
	}

	return IndexedPost{}, false, fmt.Errorf("selected post was not found in cache")
}

func init() {
	rootCmd.SetVersionTemplate("{{.Version}}\n")

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
		Short: "Open a blog post by date, or fuzzy-pick one when no date is given",
		Args:  cobra.MaximumNArgs(1),
		RunE:  openPost,
	}

	listCmd := &cobra.Command{
		Use:     "list",
		Aliases: []string{"ls", "posts"},
		Short:   "List cached blog posts",
		RunE:    listPosts,
	}
	listCmd.Flags().BoolP("refresh", "r", false, "Refresh the post cache before listing")
	listCmd.Flags().IntP("limit", "n", 50, "Maximum number of posts to list (0 for all)")
	listCmd.Flags().Bool("all", false, "List all posts")

	findCmd := &cobra.Command{
		Use:     "find [query]",
		Aliases: []string{"search", "fzf"},
		Short:   "Fuzzy search posts and open the selected result",
		Args:    cobra.ArbitraryArgs,
		RunE:    findPost,
	}
	findCmd.Flags().BoolP("refresh", "r", false, "Refresh the post cache before searching")

	cacheCmd := &cobra.Command{
		Use:   "cache",
		Short: "Manage the post cache",
	}
	cacheRefreshCmd := &cobra.Command{
		Use:   "refresh",
		Short: "Rebuild the post cache",
		RunE: func(cmd *cobra.Command, args []string) error {
			index, err := refreshCache(true)
			if err != nil {
				return err
			}
			cachePath, _ := cacheFile()
			fmt.Printf("Cached %d posts at %s\n", len(index.Posts), cachePath)
			return nil
		},
	}
	cachePathCmd := &cobra.Command{
		Use:   "path",
		Short: "Print the post cache path",
		RunE: func(cmd *cobra.Command, args []string) error {
			cachePath, err := cacheFile()
			if err != nil {
				return err
			}
			fmt.Println(cachePath)
			return nil
		},
	}
	cacheCmd.AddCommand(cacheRefreshCmd, cachePathCmd)

	rootCmd.AddCommand(newCmd, openCmd, listCmd, findCmd, cacheCmd)
}

func newPost(cmd *cobra.Command, args []string) error {
	if err := requireBaseDir(); err != nil {
		return err
	}

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
	if _, err := refreshCache(false); err != nil {
		fmt.Printf("Warning: post was created, but cache refresh failed: %v\n", err)
	}

	fmt.Printf("Opening %s\n", file)
	if err := openFile(file); err != nil {
		fmt.Printf("Error opening file %s: %v\n", file, err)
	}
	return openFile(path)
}

func parseDate(input string) (time.Time, error) {
	input = strings.TrimSpace(strings.NewReplacer("/", "-", ".", "-").Replace(input))
	formats := []string{
		"2006-01-02",
		"Jan. 02, 2006",
		"January 02, 2006",
	}

	for _, format := range formats {
		if t, err := time.Parse(format, input); err == nil {
			return t.Add(12 * time.Hour), nil
		}
	}
	return time.Time{}, fmt.Errorf("no valid date format found for: %s", input)
}

func openPost(cmd *cobra.Command, args []string) error {
	if len(args) == 0 {
		return findPost(cmd, nil)
	}

	if err := requireBaseDir(); err != nil {
		return err
	}

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

func listPosts(cmd *cobra.Command, args []string) error {
	refresh, _ := cmd.Flags().GetBool("refresh")
	limit, _ := cmd.Flags().GetInt("limit")
	all, _ := cmd.Flags().GetBool("all")
	if all {
		limit = 0
	}

	index, err := loadCache(refresh)
	if err != nil {
		return err
	}

	count := len(index.Posts)
	if limit > 0 && limit < count {
		count = limit
	}
	for _, post := range index.Posts[:count] {
		fmt.Println(formatPost(post))
	}
	return nil
}

func findPost(cmd *cobra.Command, args []string) error {
	refresh, _ := cmd.Flags().GetBool("refresh")
	query := strings.Join(args, " ")

	index, err := loadCache(refresh)
	if err != nil {
		return err
	}

	post, ok, err := selectPost(index.Posts, query)
	if err != nil || !ok {
		return err
	}

	fmt.Printf("Opening %s\n", post.Path)
	if err := openFile(post.Path); err != nil {
		return err
	}
	return openFile(filepath.Dir(post.Path))
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
