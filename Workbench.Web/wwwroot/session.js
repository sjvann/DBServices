window.wbSetSession = (id) => {
  document.cookie = "wb_session=" + id + "; path=/; samesite=lax";
};
window.wbClearSession = () => {
  document.cookie = "wb_session=; path=/; max-age=0";
};
