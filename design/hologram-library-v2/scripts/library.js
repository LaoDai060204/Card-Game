/* Classic scripts keep per-card payloads usable from an extracted offline ZIP. */
(function () {
  'use strict';
  const pending = new Map();
  window.BSCardLibrary = {
    load(id) {
      if (window.BS_PAYLOADS[id]) return Promise.resolve(window.BS_PAYLOADS[id]);
      if (pending.has(id)) return pending.get(id);
      const card = window.BS_CARDS[id];
      if (!card || !/^[A-Za-z0-9-]+$/.test(card.code)) return Promise.reject(new Error('Unknown card.'));
      const promise = new Promise((resolve, reject) => {
        const script = document.createElement('script');
        const timer = setTimeout(() => finish(new Error('The card took too long to load. Choose it again to retry.')), 20000);
        let done = false;
        function finish(error) {
          if (done) return; done = true; clearTimeout(timer); script.remove(); pending.delete(id);
          if (error) reject(error); else resolve(window.BS_PAYLOADS[id]);
        }
        script.src = 'cards/' + card.code + '.js';
        script.onload = () => finish(window.BS_PAYLOADS[id] ? null : new Error('The card payload is incomplete.'));
        script.onerror = () => finish(new Error('Cannot load this card. Extract the full color ZIP, then choose it again.'));
        document.head.appendChild(script);
      });
      pending.set(id, promise); return promise;
    },
    release(id) { delete window.BS_PAYLOADS[id]; }
  };
})();
