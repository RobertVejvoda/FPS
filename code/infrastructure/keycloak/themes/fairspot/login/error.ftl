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
<div class="fps-wrapper">
  <main class="fps-card" role="main">

    <header class="fps-brand">
      <div class="fps-logo-mark" aria-hidden="true">F</div>
      <div class="fps-brand-text">
        <span class="fps-brand-name">FairSpot</span>
        <span class="fps-brand-tagline">Fair parking for everyone</span>
      </div>
    </header>

    <span class="fps-error-icon" aria-hidden="true">⚠️</span>
    <h1 class="fps-title" style="text-align:center;">${msg("errorTitle")}</h1>

    <#if message?has_content>
      <div class="fps-alert fps-alert-error" role="alert">
        ${kcSanitize(message.summary)?no_esc}
      </div>
    </#if>

    <#if client?? && client.baseUrl?has_content>
      <a class="fps-btn-secondary" href="${client.baseUrl}">${msg("backToApplication")}</a>
    </#if>

  </main>

  <footer class="fps-footer">
    <p>${msg("fps.supportHint")}</p>
  </footer>
</div>
</body>
</html>
