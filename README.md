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
poster: cover-image.jpg
draft: true|false #make-post-available-but-hidden-on-front-page
---
This is some awesome content

:::gallery

![img](img1.jpg)

![img](img2.jpg)
:::
```

## Authentication

Its possible to lock down the site with a password that is set with the environment variable `PASSWORD=sUp3rS3cr34P4ss!`. If this variable is empty, then authorization is turned off. To persist login sessions between server restarts mount `/config` folder to the container at initialization.

## Environment variables

These are listed in [picoblog/Config.cs at main · jonasbg/picoblog · GitHub](https://github.com/jonasbg/picoblog/blob/main/Models/Config.cs)

| Variable name        | Default value | Description                                                                                                             |
| --------------------:|:-------------:| ----------------------------------------------------------------------------------------------------------------------- |
| CONFIG_DIR           | `/config`     | When using `PASSWORD` env variable, mount a `CONFIG_DIR` so that logins are persisted between container restarts.        |
| DATA_DIR             | `/data`       | This is the data path inside the container that Picoblog will traverse for markdown files.                               |
| DOMAIN               | `localhost`   | This is primarily to support the Open Graph Protocol and link previews of your site.                                      |
| PASSWORD             | `empty`       | Password protected site is off by default. Turn it on by inserting any value for this environment.                       |
| PICOBLOG_ENABLE_BACKUP | `false`      | Enables automatic daily backups of posts. Set to `true` to enable, `false` to disable.                                    |
| SYNOLOGY_SIZE        | `XL`          | Synology creates default optimized images of your photos. Available sizes are `SM`,`M` and `XL`.                           |
| SYNOLOGY_SUPPORT     | `false`       | Synology Support is turned off by default, and it will automatically fallback if `@eaDir` is not found.                   |
| TITLE     | `Picoblog`       | The title of your blog and RSS feed                   |
| DESCRIPTION     | `A simple zero fraction blogging platform`       | The description of your blog and RSS feed                   |

# Debug and contribute
<details>
  <summary>Docker Compose</summary>

```bash
docker compose up --build webapi
```

</details>

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
docker run -d -p 8080:8080 --cap-drop ALL --read-only -e DOMAIN=pico.blog --name picoblog --volume /image/directory:/data:ro jonasbg/picoblog
```
Or with password enabled. Tmpfs is mounted as an example. Persist that to a directory on the host to perist login sessions.
```bash
docker run -d -p 8080:8080 --cap-drop ALL --read-only -e PASSWORD="myPassword" -e DOMAIN=pico.blog --name picoblog --mount type=tmpfs,destination=/config --volume /image/directory:/data:ro jonasbg/picoblog
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

Install
```bash
helm install picoblog oci://ghcr.io/jonasbg/picoblog/picoblog
```

Download latest version
```bash
helm pull oci://ghcr.io/jonasbg/picoblog/picoblog
``````

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

- [x] ~~Search for `.md` files at a given interval or events. (Background task initiated on site request within a given interval ie. once every 5min if theres traffic to the site)~~

- [x] ~~Better mobile responsive support~~

- [ ] Tags in posts. This is not implemented but will be `tags: ["tag1", "tag2"]`

- [x] ~~Image optimization where `SYNOLOGY_SUPPORT` is unavailable - *or Synology optimized imagere are **too** optimized for your taste*~~__Note: HEIC is not supported__

- [ ] Big Picture and carousel mode with basic `EXIF`support such as

  - [ ] Camera model

  - [ ] Lens model

  - [ ] Shutter speed

  - [ ] Aperture

  - [ ] ISO

  - [ ] Location on a mini map

  - [ ] Description

  - [ ] Tags

- [x] ~~Virtual directory with assets imported from `*.md` files as a security measurement.~~
