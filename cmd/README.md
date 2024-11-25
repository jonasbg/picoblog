# 🚀 PicoBlog CLI

A command-line tool for managing markdown blog posts in a structured directory format. Written in Go, compiled as a native executable that runs on macOS and Linux.

## ⚙️ Prerequisites

For building:
- Go 1.21 or later

For running:
- No prerequisites! The binary is statically linked

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
# Open post by date
picoblog open "2024-01-20"

# Alternative date formats
picoblog open "Jan. 20, 2024"
picoblog open "January 20, 2024"
picoblog open "2024/01/20"
picoblog open "2024.01.20"
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
  date                   Date of the blog post (YYYY-MM-DD or MMM. DD, YYYY)

Options:
  -h, --help            Show help for the open command
```

## ⚡️ Features

- ✅ Native binary - no runtime dependencies
- ✅ Structured directory organization
- ✅ Markdown file generation with front matter
- ✅ Support for public/private and draft posts
- ✅ Automatic file opening (uses `open` on macOS, `xdg-open` on Linux)
- ✅ Multiple date format support
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