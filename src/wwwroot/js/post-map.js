(function () {
  function parseJson(element) {
    try {
      return JSON.parse(element.textContent || "null");
    } catch {
      return null;
    }
  }

  function postCardHtml(post) {
    const article = document.createElement("article");
    article.className = "map-popup-card";

    if (post.coverImageUrl) {
      const image = document.createElement("img");
      image.className = "map-popup-image";
      image.src = post.coverImageUrl;
      image.alt = "";
      image.loading = "lazy";
      article.appendChild(image);
    }

    const body = document.createElement("div");
    body.className = "map-popup-body";

    const link = document.createElement("a");
    link.className = "map-popup-title";
    link.href = post.url;
    link.textContent = post.title;

    body.appendChild(link);

    if (post.date) {
      const date = document.createElement("div");
      date.className = "map-popup-date";
      date.textContent = post.date;
      body.appendChild(date);
    }

    if (post.description) {
      const description = document.createElement("p");
      description.className = "map-popup-description";
      description.textContent = post.description;
      body.appendChild(description);
    }

    if (post.locationTitle) {
      const location = document.createElement("div");
      location.className = "map-popup-location";
      location.textContent = post.locationTitle;
      body.appendChild(location);
    }

    article.appendChild(body);
    return article;
  }

  function popupHtml(marker) {
    const wrapper = document.createElement("div");
    wrapper.className = "map-popup-stack";

    marker.posts.forEach(function (post) {
      wrapper.appendChild(postCardHtml(post));
    });

    return wrapper;
  }

  function createMap(container, posts, options) {
    if (!window.L || !container || !posts.length) {
      return;
    }

    const map = L.map(container);
    L.tileLayer(options.tileUrl, {
      maxZoom: 19,
      attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
    }).addTo(map);

    const markers = posts.map(function (post) {
      const marker = L.marker([post.latitude, post.longitude]).addTo(map);
      marker.bindPopup(popupHtml({
        latitude: post.latitude,
        longitude: post.longitude,
        posts: [post]
      }));
      return marker;
    });

    if (markers.length === 1) {
      map.setView(markers[0].getLatLng(), options.defaultZoom || 13);
      markers[0].openPopup();
      return;
    }

    const group = L.featureGroup(markers);
    map.fitBounds(group.getBounds(), { padding: [30, 30], maxZoom: options.defaultZoom || 13 });
  }

  function createDynamicMap(container, options) {
    if (!window.L || !container || !options.dynamicUrl) {
      return;
    }

    const map = L.map(container).setView([61.0014937, 11.1647964], options.defaultZoom || 13);
    const layer = L.layerGroup().addTo(map);
    let requestId = 0;
    let hasLoaded = false;

    L.tileLayer(options.tileUrl, {
      maxZoom: 19,
      attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
    }).addTo(map);

    async function loadMarkers() {
      const currentRequest = ++requestId;
      const bounds = map.getBounds();
      const url = new URL(options.dynamicUrl, window.location.origin);
      url.searchParams.set("north", bounds.getNorth());
      url.searchParams.set("south", bounds.getSouth());
      url.searchParams.set("east", bounds.getEast());
      url.searchParams.set("west", bounds.getWest());

      const response = await fetch(url);
      if (!response.ok || currentRequest !== requestId) {
        return;
      }

      const markers = await response.json();
      if (currentRequest !== requestId) {
        return;
      }

      layer.clearLayers();
      markers.forEach(function (markerData) {
        const marker = L.marker([markerData.latitude, markerData.longitude]).addTo(layer);
        marker.bindPopup(popupHtml(markerData));
      });

      if (!hasLoaded && markers.length > 0) {
        hasLoaded = true;
        const leafletMarkers = layer.getLayers();
        if (leafletMarkers.length === 1) {
          map.setView(leafletMarkers[0].getLatLng(), options.defaultZoom || 13);
          leafletMarkers[0].openPopup();
        } else {
          map.fitBounds(L.featureGroup(leafletMarkers).getBounds(), {
            padding: [30, 30],
            maxZoom: options.defaultZoom || 13
          });
        }
      }
    }

    map.on("moveend", loadMarkers);
    loadMarkers();
  }

  document.addEventListener("DOMContentLoaded", function () {
    document.querySelectorAll("[data-post-map]").forEach(function (container) {
      const dataElement = document.getElementById(container.dataset.postMap);
      const data = dataElement ? parseJson(dataElement) : null;
      if (!data) {
        return;
      }

      if (data.dynamicUrl) {
        createDynamicMap(container, {
          tileUrl: data.tileUrl,
          defaultZoom: data.defaultZoom,
          dynamicUrl: data.dynamicUrl
        });
        return;
      }

      if (!Array.isArray(data.posts)) {
        return;
      }

      createMap(container, data.posts, {
        tileUrl: data.tileUrl,
        defaultZoom: data.defaultZoom
      });
    });
  });
})();
