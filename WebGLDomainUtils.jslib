mergeInto(LibraryManager.library, {

  GetWebGLOrigin: function() {
    try {
      return window.location.origin;
    } catch (e) {
      console.warn('Failed to get window.location.origin:', e);
      return "";
    }
  },

});

