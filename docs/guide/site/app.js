(() => {
  const TAB_IDS = ["welcome", "loops", "features", "mechanisms", "play", "stakes"];
  const STORAGE_KEY = "ros-guide-tab";

  const tabs = Array.from(document.querySelectorAll('[role="tab"]'));
  const panels = Array.from(document.querySelectorAll('[role="tabpanel"]'));

  function normalizeId(raw) {
    if (!raw) return null;
    const id = raw.replace(/^#/, "").toLowerCase();
    return TAB_IDS.includes(id) ? id : null;
  }

  function activate(id, { focusTab = false, updateHash = true } = {}) {
    const next = normalizeId(id) || "welcome";

    tabs.forEach((tab) => {
      const selected = tab.dataset.tab === next;
      tab.setAttribute("aria-selected", selected ? "true" : "false");
      tab.tabIndex = selected ? 0 : -1;
      if (selected && focusTab) tab.focus();
    });

    panels.forEach((panel) => {
      const on = panel.id === `panel-${next}`;
      panel.classList.toggle("is-active", on);
      panel.hidden = !on;
    });

    try {
      sessionStorage.setItem(STORAGE_KEY, next);
    } catch {
      /* private mode */
    }

    if (updateHash) {
      const url = new URL(window.location.href);
      url.hash = next;
      history.replaceState(null, "", url);
    }
  }

  function initialTab() {
    return (
      normalizeId(window.location.hash) ||
      normalizeId(sessionStorage.getItem(STORAGE_KEY)) ||
      "welcome"
    );
  }

  tabs.forEach((tab, index) => {
    tab.addEventListener("click", () => {
      activate(tab.dataset.tab, { focusTab: true });
    });

    tab.addEventListener("keydown", (event) => {
      let targetIndex = null;
      switch (event.key) {
        case "ArrowRight":
        case "ArrowDown":
          targetIndex = (index + 1) % tabs.length;
          break;
        case "ArrowLeft":
        case "ArrowUp":
          targetIndex = (index - 1 + tabs.length) % tabs.length;
          break;
        case "Home":
          targetIndex = 0;
          break;
        case "End":
          targetIndex = tabs.length - 1;
          break;
        case "Enter":
        case " ":
          event.preventDefault();
          activate(tab.dataset.tab, { focusTab: true });
          return;
        default:
          return;
      }
      event.preventDefault();
      const nextTab = tabs[targetIndex];
      nextTab.focus();
      activate(nextTab.dataset.tab, { focusTab: false });
    });
  });

  document.querySelectorAll("[data-open-tab]").forEach((el) => {
    el.addEventListener("click", (event) => {
      event.preventDefault();
      activate(el.getAttribute("data-open-tab"), { focusTab: true });
    });
  });

  window.addEventListener("hashchange", () => {
    const fromHash = normalizeId(window.location.hash);
    if (fromHash) activate(fromHash, { updateHash: false });
  });

  activate(initialTab(), { focusTab: true, updateHash: Boolean(window.location.hash) });
})();
