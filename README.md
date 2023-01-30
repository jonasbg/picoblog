# PicoBlog

PicoBlog is a zero config blogging tool, where all you have to do is to point it to a directory and it will scan recursively for *.md files.
If some of these files is marked as `public: true` then it will make it available.

*This is not a static generator*.

Example of a markdown file
```markdown
---
title: New blog post
date: 2023-01-29
public: true
poster: poster-image.jpg
hidden: make-post-available-but-hidden-on-front-page
---
This is some awesome content

:::gallery

![img](img1.jpg)

![img](img2.jpg)
:::
```
# Install
## Docker
### Build
Build it with docker
```bash
docker build . -t jonasbg/picoblog
```
### Run
Run it:

```bash
docker run -d -e DOMAIN=pico.blog --name picoblog --volume /image/directory:/data:ro jonasbg/picoblog
```

## HELM
Install from HELM
```bash
helm repo add picoblog https://jonasbg.github.io/picoblog
helm repo install picoblog/picoblog --name picoblog
```
