<#-- FairSpot branded front-channel logout progress page. -->
<!DOCTYPE html>
<html lang="${(locale.currentLanguageTag)!'en'}">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="robots" content="noindex, nofollow">
  <title>${msg("frontchannel-logout.title")}</title>
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

    <h1 class="fps-title">${msg("frontchannel-logout.title")}</h1>
    <p class="fps-page-copy">${msg("frontchannel-logout.message")}</p>

    <#list logout.clients as client>
      <iframe title="${client.name}" src="${client.frontChannelLogoutUrl}" hidden></iframe>
    </#list>

    <#if logout.logoutRedirectUri?has_content>
      <script>
        <#outputformat "JavaScript">
        function readystatechange() {
          if (document.readyState === "complete") {
            window.location.replace(${logout.logoutRedirectUri?c});
          }
        }
        document.addEventListener("readystatechange", readystatechange);
        </#outputformat>
      </script>
      <a id="continue" class="fps-btn" href="${logout.logoutRedirectUri}">${msg("doContinue")}</a>
    </#if>

  </main>

  <footer class="fps-footer">
    <p>${msg("fps.supportHint")}</p>
  </footer>
</div>
</body>
</html>
