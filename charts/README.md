# How to build
```bash
ch charts
helm package picoblog
cd ..
helm repo index --url https://jonasbg.github.io/picoblog/charts --merge index.yaml charts
```
