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
<div class="fps-wrapper">
  <main class="fps-card" role="main">

    <header class="fps-brand">
      <div class="fps-logo-mark" aria-hidden="true">F</div>
      <div class="fps-brand-text">
        <span class="fps-brand-name">FairSpot</span>
        <span class="fps-brand-tagline">Fair parking for everyone</span>
      </div>
    </header>

    <h1 class="fps-title">${msg("logoutConfirmTitle")}</h1>
    <p class="fps-page-copy">${msg("logoutConfirmHeader")}</p>

    <form action="${url.logoutConfirmAction}" method="post" onsubmit="confirmLogout.disabled = true; return true;">
      <input type="hidden" name="session_code" value="${logoutConfirm.code}">
      <button class="fps-btn" name="confirmLogout" id="kc-logout" type="submit">
        ${msg("doLogout")}
      </button>
    </form>

    <#if !logoutConfirm.skipLink && (client.baseUrl)?has_content>
      <a class="fps-btn-secondary" href="${client.baseUrl}">${msg("backToApplication")}</a>
    </#if>

  </main>

  <footer class="fps-footer">
    <p>${msg("fps.supportHint")}</p>
  </footer>
</div>
</body>
</html>
