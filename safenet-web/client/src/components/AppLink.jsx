// Changes pages inside React without reloading the entire website.
export default function AppLink({ to, children, ...props }) {
  function navigate(event) {
    if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    window.history.pushState({}, '', to);
    window.dispatchEvent(new PopStateEvent('popstate'));
  }

  return <a href={to} onClick={navigate} {...props}>{children}</a>;
}
