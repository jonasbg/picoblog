likeButton.addEventListener('click', async () => {
  try {
      const response = await fetch(window.location.pathname + '/like');
      const data = await response.json();
      if (data.success) {
          document.getElementById('likeCount').textContent = data.likes;
          toggleLikeButton();
          updateLocalStorageLike();
      }
  } catch (err) {
      console.error('Error liking post:', err);
  }
});

function toggleLikeButton() {
  const icon = document.getElementById('like-icon');
  const iconFill = document.getElementById('like-icon-fill');
  icon.classList.toggle('hidden');
  iconFill.classList.toggle('hidden');
}

function updateLocalStorageLike() {
  const path = window.location.pathname;
  const liked = localStorage.getItem(`liked-${path}`) === 'true';
  localStorage.setItem(`liked-${path}`, (!liked).toString());
}

// Check if post was previously liked
function setInitialLikeState() {
  const path = window.location.pathname;
  const liked = localStorage.getItem(`liked-${path}`) === 'true';
  if (liked) {
      toggleLikeButton();
  }
}

setInitialLikeState();