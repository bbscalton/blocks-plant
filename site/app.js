(function () {
  "use strict";

  var stock = { "4": 1240, "6": 860, "8": 410 };
  var cementBags = 18;

  var sizeEl = document.getElementById("pos-size");
  var qtyEl = document.getElementById("pos-qty");
  var totalEl = document.getElementById("pos-total");
  var pulseMsg = document.getElementById("pulse-msg");

  function formatMoney(n) {
    return "$" + n.toFixed(2);
  }

  function updatePosTotal() {
    if (!sizeEl || !qtyEl || !totalEl) return;
    var price = parseFloat(sizeEl.value) || 0;
    var qty = Math.max(0, parseInt(qtyEl.value, 10) || 0);
    totalEl.textContent = formatMoney(price * qty);
  }

  function renderStock() {
    document.querySelectorAll("[data-stock]").forEach(function (el) {
      var key = el.getAttribute("data-stock");
      if (stock[key] != null) {
        el.textContent = stock[key].toLocaleString("en-US");
      }
    });
    var mat = document.querySelector("[data-mat='cement']");
    if (mat) {
      mat.textContent = cementBags + " bags";
      var li = mat.closest("li");
      if (li) {
        li.classList.toggle("warn", cementBags < 25);
      }
    }
  }

  function setPulse(msg) {
    if (pulseMsg) pulseMsg.textContent = msg;
  }

  if (sizeEl) sizeEl.addEventListener("change", updatePosTotal);
  if (qtyEl) qtyEl.addEventListener("input", updatePosTotal);

  var btnProduce = document.getElementById("btn-produce");
  var btnSell = document.getElementById("btn-sell");
  var btnReset = document.getElementById("btn-reset");

  if (btnProduce) {
    btnProduce.addEventListener("click", function () {
      stock["4"] += 80;
      cementBags = Math.max(0, cementBags - 3);
      renderStock();
      setPulse("Logged +80 × 4″ and deducted sample cement from materials.");
    });
  }

  if (btnSell) {
    btnSell.addEventListener("click", function () {
      if (stock["6"] < 40) {
        setPulse("Not enough 6″ on hand for that sale — same rule the live API enforces.");
        return;
      }
      stock["6"] -= 40;
      renderStock();
      setPulse("Counter sale of 40 × 6″ deducted from finished stock.");
    });
  }

  if (btnReset) {
    btnReset.addEventListener("click", function () {
      stock = { "4": 1240, "6": 860, "8": 410 };
      cementBags = 18;
      renderStock();
      setPulse("Snapshot reset to sample starting numbers.");
    });
  }

  // Point repo CTAs at this GitHub Pages project when hosted under github.io/<repo>/
  var pathParts = location.pathname.split("/").filter(Boolean);
  var repoLink = document.getElementById("repo-link");
  var readmeLink = document.getElementById("readme-link");
  if (location.hostname.endsWith("github.io") && pathParts.length >= 1) {
    var owner = location.hostname.replace(".github.io", "");
    var repo = pathParts[0];
    var repoUrl = "https://github.com/" + owner + "/" + repo;
    if (repoLink) repoLink.href = repoUrl;
    if (readmeLink) readmeLink.href = repoUrl + "#readme";
  } else {
    // Local file / preview: relative README is not available; leave # or guess common path
    if (repoLink) repoLink.href = "../README.md";
    if (readmeLink) readmeLink.href = "../README.md";
  }

  updatePosTotal();
  renderStock();
})();
