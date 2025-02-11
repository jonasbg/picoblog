#!/bin/bash

# Exit on any error
set -e

# Get the version from git tag if available, otherwise use "dev"
VERSION=$(git describe --tags 2>/dev/null || echo "dev")

# Common build flags
BUILD_FLAGS="-buildvcs=false -ldflags \"-X main.Version=${VERSION} -s -w\""

# Clean previous builds
rm -rf dist/
mkdir -p dist

# Build function
build() {
    local os=$1
    local arch=$2
    local output_dir="dist/${os}-${arch}"
    local binary_name="picoblog"

    # Add .exe extension for Windows
    if [ "$os" = "windows" ]; then
        binary_name="picoblog.exe"
    fi

    echo "Building picoblog ${VERSION} for ${os}/${arch}..."
    mkdir -p "${output_dir}"

    GOOS=$os GOARCH=$arch CGO_ENABLED=0 \
    go build -buildvcs=false \
        -ldflags "-X main.Version=${VERSION} -s -w" \
        -o "${output_dir}/${binary_name}"

    chmod +x "${output_dir}/${binary_name}"
}

# Build for all targets
build "darwin" "arm64"  # macOS ARM (M1/M2)
build "darwin" "amd64"  # macOS Intel
build "linux" "arm64"   # Linux ARM64
build "linux" "amd64"   # Linux AMD64

# Create archives for each build
echo "Creating archives..."
cd dist

for dir in *; do
    if [ -d "$dir" ]; then
        tar -czf "${dir}.tar.gz" "${dir}"
        echo "Created ${dir}.tar.gz"
    fi
done

cd ..

# Print results
echo ""
echo "✨ Build complete! Binaries are in dist/ directory:"
ls -l dist/

echo ""
echo "Archives created:"
ls -l dist/*.tar.gz

echo ""
echo "To install on macOS ARM (M1/M2), use:"
echo "sudo cp dist/darwin-arm64/picoblog /usr/local/bin/"

echo ""
echo "To install on macOS Intel, use:"
echo "sudo cp dist/darwin-amd64/picoblog /usr/local/bin/"

echo ""
echo "To install on Linux, use:"
echo "sudo cp dist/linux-<arch>/picoblog /usr/local/bin/"
echo "Where <arch> is amd64 or arm64 depending on your system"