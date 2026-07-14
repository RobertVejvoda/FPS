<#-- FairSpot branded logout confirmation page. -->
<!DOCTYPE html>
<html lang="${(locale.currentLanguageTag)!'en'}">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="robots" content="noindex, nofollow">
  <title>${msg("logoutConfirmTitle")}</title>
  <link rel="stylesheet" href="${url.resourcesPath}/css/login.css">
</head>
<body>
<div class="fairspot-wrapper">
  <main class="fairspot-card" role="main">

    <header class="fairspot-brand">
      <div class="fairspot-logo-mark" aria-hidden="true">F</div>
      <div class="fairspot-brand-text">
        <span class="fairspot-brand-name">FairSpot</span>
        <span class="fairspot-brand-tagline">${msg("fairspotTagline")}</span>
      </div>
    </header>

    <h1 class="fairspot-title">${msg("logoutConfirmTitle")}</h1>
    <p class="fairspot-page-copy">${msg("logoutConfirmHeader")}</p>

    <form action="${url.logoutConfirmAction}" method="post" onsubmit="confirmLogout.disabled = true; return true;">
      <input type="hidden" name="session_code" value="${logoutConfirm.code}">
      <button class="fairspot-btn" name="confirmLogout" id="kc-logout" type="submit">
        ${msg("doLogout")}
      </button>
    </form>

    <#if !logoutConfirm.skipLink && (client.baseUrl)?has_content>
      <a class="fairspot-btn-secondary" href="${client.baseUrl}">${msg("backToApplication")}</a>
    </#if>

  </main>

  <footer class="fairspot-footer">
    <p>${msg("fps.supportHint")}</p>
  </footer>
</div>
</body>
</html>
