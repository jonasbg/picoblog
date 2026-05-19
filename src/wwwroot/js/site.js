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

document.addEventListener("DOMContentLoaded", enhancePostGalleries);
