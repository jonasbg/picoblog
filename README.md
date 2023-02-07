# 📄 PicoBlog 📄

PicoBlog is a *zero config* policy website blog-post application, where all you have to do is to point it to a directory and it will scan recursively for any *.md files at startup.
Those marked with `public: true` inside the header, will be made available.

*This is not a static generator*.

Build with **.NET MVC** - *because I'm too old for the other cool site generators.*

**A beautiful home with huge previews of the post**

![](.github/docs/images/home.gif)

**Posts with a beautiful photo gallery**

![](.github/docs/images/post.gif)

Example of a markdown file

```markdown
---
title: New blog post #required uniq title
date: 2023-01-29 #published date
public: true #keyword for picoblog to publish as post
poster: poster-image.jpg
draft: true|false #make-post-available-but-hidden-on-front-page
---
This is some awesome content

:::gallery

![img](img1.jpg)

![img](img2.jpg)
:::
```

## Authentication

Its possible to lock down the site with a password that is set with the environment variable `PASSWORD=sUp3rS3cr34P4ss!`. If this variable is empty, then authorization is turned off. The login session is not persisted between server restarts.

## Environment variables

These are listed in [picoblog/Config.cs at main · jonasbg/picoblog · GitHub](https://github.com/jonasbg/picoblog/blob/main/Models/Config.cs)

| Variable name    | Default value | Description                                                                                            |
| ----------------:|:-------------:| ------------------------------------------------------------------------------------------------------ |
| SYNOLOGY_SUPPORT | `true`        | Synology Support is turned on by default, and it will automatically fallback if `@eaDir` is not found. |
| PASSWORD         | `empty`       | Password protected site is off by default. Turn it on by inserting any value for this environment.     |
| DATA_DIR         | `/data`       | This is the data path inside the container that Picoblog will traverse for markdown files.             |
| DOMAIN           | `localhost`   | This is primarly to support the Open Graph Protocol and link previews of your site.                    |
| SYNOLOGY_SIZE    | `XL`          | Synology creates default optimized images of your photos. Available sizes are `SM`,`M` and `XL`.       |

# Install

<details>
  <summary>Build it yourself</summary>

```bash
docker build . -t jonasbg/picoblog
```

</details>

<details>
  <summary>Docker</summary>

  The latest build will always be uploaded to dockerhub so download it from there.

```bash
docker run -d -p 8080:8080 -e DOMAIN=pico.blog --name picoblog --volume /image/directory:/data:ro jonasbg/picoblog
```

  Open ➡ [localhost:8080](http://localhost:8080).

### Restart
Update the docker image to latest version

```bash
docker run --rm -v /var/run/docker.sock:/var/run/docker.sock containrrr/watchtower --run-once picoblog
```

</details>

<details>
  <summary>HELM</summary>

```bash
helm repo add picoblog https://jonasbg.github.io/picoblog
helm repo install picoblog/picoblog --name picoblog
```

### Restart
```bash
kubectl rollout restart deployment/picoblog -n picoblog && kubectl rollout status deployment/picoblog -n picoblog
```

</details>

# Extra commands

Some commands that are nice to know about

Find all your drafts for easy editing:

```bash
find . -name "*.md" -exec sh -c "grep -H 'draft' '{}'"  \;
```

Backup your markdown files with git

```bash
git add *.md
git commit -m "Backup of markdown files"
```

# Todos

Some important work remains

- [ ] Search for `.md` files at a given interval or events. (Background task initiated on site request within a given interval ie. once every 5min if theres traffic to the site)

- [x] ~~Better mobile responsive support~~

- [ ] Tags in posts. This is not implemented but will be `tags: ["tag1", "tag2"]`

- [ ] Image optimization where `SYNOLOGY_SUPPORT` is unavailable - *or Synology optimized imagere are **too** optimized for your taste*

- [ ] Big Picture and carousel mode with basic `EXIF`support such as

  - [ ] Camera model

  - [ ] Lens model

  - [ ] Shutter speed

  - [ ] Aperture

  - [ ] ISO

  - [ ] Location on a mini map

  - [ ] Description

  - [ ] Tags

- [x] ~Virtual directory with assets imported from `*.md` files as a security measurement.~~
