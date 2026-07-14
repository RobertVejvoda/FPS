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
<div class="fairspot-wrapper">
  <main class="fairspot-card" role="main">

    <header class="fairspot-brand">
      <div class="fairspot-logo-mark" aria-hidden="true">F</div>
      <div class="fairspot-brand-text">
        <span class="fairspot-brand-name">FairSpot</span>
        <span class="fairspot-brand-tagline">${msg("fairspotTagline")}</span>
      </div>
    </header>

    <h1 class="fairspot-title">${msg("frontchannel-logout.title")}</h1>
    <p class="fairspot-page-copy">${msg("frontchannel-logout.message")}</p>

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
      <a id="continue" class="fairspot-btn" href="${logout.logoutRedirectUri}">${msg("doContinue")}</a>
    </#if>

  </main>

  <footer class="fairspot-footer">
    <p>${msg("fps.supportHint")}</p>
  </footer>
</div>
</body>
</html>
