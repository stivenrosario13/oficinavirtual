(function () {
  "use strict";
  var release = "V325";
  var storageKey = "real-agencias-active-release";

  function reloadWithRelease() {
    var url = new URL(window.location.href);
    if (url.searchParams.get("_release") === release) return;
    url.searchParams.set("_release", release);
    window.location.replace(url.toString());
  }

  window.addEventListener("vite:preloadError", function (event) {
    event.preventDefault();
    reloadWithRelease();
  });

  try {
    if (window.localStorage.getItem(storageKey) === release) return;
    window.localStorage.setItem(storageKey, release);
    var cleanup = [];
    if ("caches" in window) {
      cleanup.push(
        window.caches.keys().then(function (keys) {
          return Promise.all(keys.map(function (key) { return window.caches.delete(key); }));
        }),
      );
    }
    if ("serviceWorker" in navigator) {
      cleanup.push(
        navigator.serviceWorker.getRegistrations().then(function (registrations) {
          return Promise.all(registrations.map(function (registration) { return registration.unregister(); }));
        }),
      );
    }
    Promise.allSettled(cleanup).finally(reloadWithRelease);
  } catch {
    reloadWithRelease();
  }
})();
