<#-- FairSpot: generic Keycloak error page -->
<!DOCTYPE html>
<html lang="${(locale.currentLanguageTag)!'en'}">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="robots" content="noindex, nofollow">
  <title>${msg("errorTitle")}</title>
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

    <span class="fairspot-error-icon" aria-hidden="true">⚠️</span>
    <h1 class="fairspot-title" style="text-align:center;">${msg("errorTitle")}</h1>

    <#if message?has_content>
      <div class="fairspot-alert fairspot-alert-error" role="alert">
        ${kcSanitize(message.summary)?no_esc}
      </div>
    </#if>

    <#if client?? && client.baseUrl?has_content>
      <a class="fairspot-btn-secondary" href="${client.baseUrl}">${msg("backToApplication")}</a>
    </#if>

  </main>

  <footer class="fairspot-footer">
    <p>${msg("fps.supportHint")}</p>
  </footer>
</div>
</body>
</html>
