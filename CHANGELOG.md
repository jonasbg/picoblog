# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.0.8] - 2023-08-05
### Added

- Introduced an optional daily backup feature controlled by the `PICOBLOG_ENABLE_BACKUP` environment variable.
- Implemented global usings through `GlobalUsings.cs` for cleaner code (if applicable to your .NET version).
- Parallel processing of markdown files during search
- Log failed login attempts
  
### Changed
- Updated the README to reflect new environment variables and features.

### Fixed
- Potential bug when finding frontmatter with the `MarkdownModel.cs` class.
- Redirection Validation: Ensure that the redirection is to a local URL to prevent open redirect attacks.
- Multiple read bug in `resize` method in `PostController`

### Chore
- Optimized the `MarkdownModel.cs` class.

## [0.0.7]
### Added
- Background polling for new files
- Image optmization support stored in memory
- Memories - view previous events on this day in history
- Favorites support - store favorites in browser

## [0.0.6] - 2023-02-06
### Added

- Progressiv Web Application Support

## [0.0.5] - 2023-02-04
### Added

- New Calendar View `/calendar`
- New Month View `/calendar/{year}`

## [0.0.4] - 2023-02-04

### Changed

- Major refactoring of `PostController.cs` - a total rewrite.

## [0.0.3] - 2023-02-02

### Added

- Globalization
  - Support for `nb-NO`
  - Support for `en`

## [0.0.2] - 2023-02-01

### Added

- Authentication

## [0.0.1] - 2023-01-30

### Added

- 1st attempt at prevention file injection
- HELM support
- New font

### Changed

- A prettier frontpage

- Lower caps on routing

### Removed

- Removed `/home` path from URL

## [0.0.0] - 2023-01-29

### Added

- Initial commit
- Synology support
- Hidden files
- Multi platform Docker build
- Link Preview Support
