// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
// navigator.setAppBadge(1).catch((error) => {
//   //Do something with the error.
// });

function enhancePostGalleries() {
  const galleries = document.querySelectorAll(".gallery, [data-gallery]");

  galleries.forEach((gallery) => {
    const images = Array.from(gallery.querySelectorAll("img"));

    if (images.length < 2) {
      return;
    }

    let classifiedCount = 0;
    let landscapeCount = 0;

    const updateGalleryPattern = () => {
      if (landscapeCount > 1 && landscapeCount >= classifiedCount / 2) {
        gallery.classList.add("gallery-landscape-heavy");
      }
    };

    const classifyImage = (image) => {
      if (image.dataset.galleryOrientation) {
        return;
      }

      const item = image.closest("p") || image.parentElement;

      if (!item || !image.naturalWidth || !image.naturalHeight) {
        return;
      }

      const width = image.naturalWidth;
      const height = image.naturalHeight;
      let orientation = "square";

      item.classList.add("gallery-item");

      if (width > height * 1.08) {
        orientation = "landscape";
        landscapeCount += 1;
      } else if (height > width * 1.08) {
        orientation = "portrait";
      }

      classifiedCount += 1;
      image.dataset.galleryOrientation = orientation;
      item.classList.add(`gallery-item-${orientation}`);
      updateGalleryPattern();
    };

    gallery.classList.add("gallery-mosaic");

    images.forEach((image) => {
      if (image.complete) {
        classifyImage(image);
      } else {
        image.addEventListener("load", () => classifyImage(image), { once: true });
      }
    });
  });
}

function enhanceNavbarSearch() {
  const input = document.getElementById("navbar-search-input");
  const results = document.getElementById("navbar-search-results");

  if (!input || !results) {
    return;
  }

  let debounceTimer = null;
  let activeRequest = null;
  let activeIndex = -1;
  let latestQuery = "";

  const hideResults = () => {
    results.hidden = true;
    results.replaceChildren();
    input.setAttribute("aria-expanded", "false");
    activeIndex = -1;
  };

  const resultLinks = () => Array.from(results.querySelectorAll(".navbar-search-result"));

  const setActiveResult = (nextIndex) => {
    const links = resultLinks();
    activeIndex = nextIndex;

    links.forEach((link, index) => {
      const isActive = index === activeIndex;
      link.classList.toggle("active", isActive);
      link.setAttribute("aria-selected", isActive ? "true" : "false");

      if (isActive) {
        link.scrollIntoView({ block: "nearest" });
      }
    });
  };

  const createCover = (result) => {
    if (result.coverImageUrl) {
      const image = document.createElement("img");
      image.className = "navbar-search-cover";
      image.src = result.coverImageUrl;
      image.alt = "";
      image.loading = "lazy";
      return image;
    }

    const placeholder = document.createElement("div");
    placeholder.className = "navbar-search-cover navbar-search-cover-placeholder";
    placeholder.textContent = (result.title || "?").trim().charAt(0).toUpperCase();
    return placeholder;
  };

  const renderResults = (items, query) => {
    results.replaceChildren();
    input.setAttribute("aria-expanded", "true");
    results.hidden = false;
    activeIndex = -1;

    if (!items.length) {
      const empty = document.createElement("div");
      empty.className = "navbar-search-empty";
      empty.textContent = `No posts found for "${query}"`;
      results.appendChild(empty);
      return;
    }

    items.forEach((result, index) => {
      const link = document.createElement("a");
      link.className = "navbar-search-result";
      link.href = result.url;
      link.id = `navbar-search-result-${index}`;
      link.setAttribute("role", "option");
      link.setAttribute("aria-selected", "false");

      const copy = document.createElement("span");
      copy.className = "navbar-search-copy";

      const title = document.createElement("strong");
      title.className = "navbar-search-title";
      title.textContent = result.title;
      copy.appendChild(title);

      if (result.excerpt) {
        const excerpt = document.createElement("span");
        excerpt.className = "navbar-search-excerpt";
        excerpt.textContent = result.excerpt;
        copy.appendChild(excerpt);
      }

      if (result.dateText) {
        const date = document.createElement("span");
        date.className = "navbar-search-date";
        date.textContent = result.dateText;
        copy.appendChild(date);
      }

      link.append(createCover(result), copy);
      results.appendChild(link);
    });
  };

  const search = async () => {
    const query = input.value.trim();
    latestQuery = query;

    if (activeRequest) {
      activeRequest.abort();
    }

    if (query.length < 2) {
      hideResults();
      return;
    }

    activeRequest = new AbortController();

    try {
      const response = await fetch(`/search/suggest?q=${encodeURIComponent(query)}`, {
        headers: { Accept: "application/json" },
        signal: activeRequest.signal,
      });

      if (!response.ok || query !== latestQuery) {
        return;
      }

      const items = await response.json();
      renderResults(items, query);
    } catch (error) {
      if (error.name !== "AbortError") {
        hideResults();
      }
    }
  };

  input.addEventListener("input", () => {
    clearTimeout(debounceTimer);
    debounceTimer = setTimeout(search, 180);
  });

  input.addEventListener("focus", () => {
    if (input.value.trim().length >= 2 && results.children.length) {
      results.hidden = false;
      input.setAttribute("aria-expanded", "true");
    }
  });

  input.addEventListener("keydown", (event) => {
    const links = resultLinks();

    if (event.key === "Escape") {
      hideResults();
      input.blur();
      return;
    }

    if (!links.length) {
      return;
    }

    if (event.key === "ArrowDown") {
      event.preventDefault();
      setActiveResult(activeIndex < links.length - 1 ? activeIndex + 1 : 0);
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      setActiveResult(activeIndex > 0 ? activeIndex - 1 : links.length - 1);
    } else if (event.key === "Enter") {
      if (!results.hidden) {
        event.preventDefault();
        links[activeIndex >= 0 ? activeIndex : 0].click();
      }
    }
  });

  document.addEventListener("click", (event) => {
    if (!input.contains(event.target) && !results.contains(event.target)) {
      hideResults();
    }
  });
}

document.addEventListener("DOMContentLoaded", enhancePostGalleries);
document.addEventListener("DOMContentLoaded", enhanceNavbarSearch);
