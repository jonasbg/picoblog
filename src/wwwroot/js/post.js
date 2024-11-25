likeButton.addEventListener('click', async () => {
  try {
      const path = window.location.pathname;
      const isLiked = localStorage.getItem(`liked-${path}`) === 'true';

      console.log(isLiked)

      const response = await fetch(`${path}/like`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json'
          },
          body: JSON.stringify({
              action: isLiked ? 'decrement' : 'increment'
          })
      });

      const data = await response.json();
      if (data.success) {
          document.getElementById('likeCount').textContent = data.likes;
          toggleLikeButton();
          updateLocalStorageLike();
      }
  } catch (err) {
      console.error('Error updating like status:', err);
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