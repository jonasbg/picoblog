# 🚀 PicoBlog CLI

A command-line tool for managing markdown blog posts in a structured directory format. Written in Go, compiled as a native executable that runs on macOS and Linux.

## ⚙️ Prerequisites

For building:
- Go 1.21 or later

For running:
- No prerequisites for basic commands. The binary is statically linked.
- Optional: install `fzf` for a better fuzzy post picker. The CLI falls back to an inline selector when `fzf` is not available.

## 🏗️ Building from Source

### Building for your current platform

```bash
# Clone the repository
git clone https://github.com/jonasbg/picoblog.git
cd picoblog

# Download dependencies
go mod download

# Build the project
go build
```

### Building for specific platforms

Use the provided build script:

```bash
# Make the build script executable
chmod +x build.sh

# Build for all platforms
./build.sh
```

This will create binaries for:
- macOS ARM64 (M1/M2/M3)
- macOS AMD64 (Intel)
- Linux ARM64
- Linux AMD64

The compiled binaries will be in `dist/[os]-[arch]/picoblog`

## 📦 Installation

1. Copy the appropriate binary to your desired location:
```bash
# For macOS M1/M2/M3
sudo cp dist/darwin-arm64/picoblog /usr/local/bin/

# For macOS Intel
sudo cp dist/darwin-amd64/picoblog /usr/local/bin/

# For Linux (choose appropriate architecture)
sudo cp dist/linux-amd64/picoblog /usr/local/bin/
```

2. Make it executable (if needed):
```bash
sudo chmod +x /usr/local/bin/picoblog
```

3. Set the required environment variable:
```bash
# Add to your ~/.bashrc or ~/.zshrc
export PICOBLOG_BASE_DIR="/path/to/your/blog/directory"
```

The CLI shows this setting in `picoblog --help`. It expects the folder to contain markdown posts organized as `YYYY/MM/DD/*.md`.

Post metadata is cached in:

```text
~/.config/picoblog/posts.json
```

If `XDG_CONFIG_HOME` is set, the cache is written to `$XDG_CONFIG_HOME/picoblog/posts.json`.
When `list`, `find`, or `open` needs to build the cache, it prints progress on stderr while scanning.
The scanner skips system and build directories such as `.git`, `eaDir`, `@eaDir`, `node_modules`, `bin`, `obj`, `dist`, `build`, and hidden directories.

## 📝 Usage

### Creating a New Blog Post

```bash
# Create a post with today's date
picoblog new "My Amazing Blog Post"

# Create a post with a specific date
picoblog new "My Amazing Blog Post" --date "2024-01-20"

# Create a private draft post
picoblog new "Draft Post" --public=false --draft=true
```

### Opening Existing Posts

```bash
# Fuzzy-pick any post, using fzf when available
picoblog open

# Open post by date
picoblog open "2024-01-20"

# Alternative date formats
picoblog open "Jan. 20, 2024"
picoblog open "January 20, 2024"
picoblog open "2024/01/20"
picoblog open "2024.01.20"
```

### Listing and Searching Posts

```bash
# List the latest 50 cached posts
picoblog list

# List all posts and refresh the cache first
picoblog list --all --refresh

# Fuzzy search cached posts and open the selected post
picoblog find
picoblog search "photo walk"

# Rebuild the cache explicitly
picoblog cache refresh

# Print the cache file path
picoblog cache path
```

## 📄 Blog Post Format

When you create a new post, it will generate a markdown file with this structure:

```markdown
---
title: My Amazing Blog Post
date: 2024-01-20
cover:
weather:
public: true
draft: false
---
```

The files are organized in a year/month/day directory structure:
```
PICOBLOG_BASE_DIR/
└── 2024/
    └── 01/
        └── 20/
            └── My-Amazing-Blog-Post.md
```

## 🚀 Command Options

### Global Options

```
--help    Show command help and exit
--version Show version information
```

### `new` Command

```
Arguments:
  title                   Title of the blog post

Options:
  --date, -d <date>      Date for the post (YYYY-MM-DD or MMM. DD, YYYY)
  --public               Set post visibility [default: true]
  --draft                Set post as draft [default: false]
  -h, --help            Show help for the new command
```

### `open` Command

```
Arguments:
  date                   Optional date of the blog post (YYYY-MM-DD or MMM. DD, YYYY)

Options:
  -h, --help            Show help for the open command
```

When no date is passed, `open` fuzzy-picks from cached posts.

### `list` Command

```
Aliases:
  ls, posts

Options:
  --refresh, -r          Refresh the cache before listing
  --limit, -n <number>   Maximum posts to list (0 for all) [default: 50]
  -h, --help             Show help for the list command
```

### `find` Command

```
Aliases:
  search, fzf

Arguments:
  query                  Optional text used to filter/preload the fuzzy picker

Options:
  --refresh, -r          Refresh the cache before searching
  -h, --help             Show help for the find command
```

### `cache` Command

```
picoblog cache refresh   Rebuild the post cache
picoblog cache path      Print the post cache path
```

## ⚡️ Features

- ✅ Native binary - no runtime dependencies
- ✅ Structured directory organization
- ✅ Markdown file generation with front matter
- ✅ Support for public/private and draft posts
- ✅ Automatic file opening (uses `open` on macOS, `xdg-open` on Linux)
- ✅ Multiple date format support
- ✅ Cached post index in `~/.config/picoblog/posts.json`
- ✅ Post listing and fuzzy search with optional `fzf`
- ✅ Timestamp preservation
- ✅ Cross-platform support (macOS ARM/Intel, Linux ARM/AMD)

## 🔧 Development

To generate development builds:

```bash
go build
```

To run tests (if added):

```bash
go test ./...
```

To format code:

```bash
go fmt ./...
```

## ❌ Uninstallation

```bash
# If installed in /usr/local/bin
sudo rm /usr/local/bin/picoblog
```

## 📝 License

This project is open source and available under the MIT License.
