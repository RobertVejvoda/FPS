<#-- FairSpot: update-password / set-new-password page -->
<!DOCTYPE html>
<html lang="${(locale.currentLanguageTag)!'en'}">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="robots" content="noindex, nofollow">
  <title>${msg("updatePasswordTitle")}</title>
  <link rel="stylesheet" href="${url.resourcesPath}/css/login.css">
</head>
<body>
<div class="fairspot-wrapper">
  <main class="fairspot-card" role="main">

    <header class="fairspot-brand">
      <div class="fairspot-logo-mark" aria-hidden="true">F</div>
      <div class="fairspot-brand-text">
        <span class="fairspot-brand-name">FairSpot</span>
        <span class="fairspot-brand-tagline">Fair parking for everyone</span>
      </div>
    </header>

    <h1 class="fairspot-title">${msg("updatePasswordTitle")}</h1>

    <#if message?has_content>
      <div class="fairspot-alert fairspot-alert-${message.type}" role="alert" aria-live="assertive">
        ${kcSanitize(message.summary)?no_esc}
      </div>
    </#if>

    <form id="kc-passwd-update-form" action="${url.loginAction}" method="post">

      <#-- Hidden username for accessibility / password managers -->
      <#if auth.showUsername??>
        <input type="hidden" id="username" name="username" value="${login.username!''}"
               autocomplete="username" readonly />
      </#if>

      <div class="fairspot-field">
        <label class="fairspot-label" for="password-new">${msg("passwordNew")}</label>
        <input class="fairspot-input<#if messagesPerField.existsError('password-new','password-confirm')> fairspot-input-error</#if>"
               type="password"
               id="password-new"
               name="password-new"
               tabindex="1"
               autocomplete="new-password"
               autofocus />
        <#if messagesPerField.existsError('password-new')>
          <span class="fairspot-field-error" aria-live="polite">
            ${kcSanitize(messagesPerField.get('password-new'))?no_esc}
          </span>
        </#if>
      </div>

      <div class="fairspot-field">
        <label class="fairspot-label" for="password-confirm">${msg("passwordConfirm")}</label>
        <input class="fairspot-input<#if messagesPerField.existsError('password-confirm')> fairspot-input-error</#if>"
               type="password"
               id="password-confirm"
               name="password-confirm"
               tabindex="2"
               autocomplete="new-password" />
        <#if messagesPerField.existsError('password-confirm')>
          <span class="fairspot-field-error" aria-live="polite">
            ${kcSanitize(messagesPerField.get('password-confirm'))?no_esc}
          </span>
        </#if>
      </div>

      <input type="hidden" name="stateChecker" value="${stateChecker}" />

      <button class="fairspot-btn" type="submit" tabindex="3">${msg("doSubmit")}</button>

    </form>

  </main>

  <footer class="fairspot-footer">
    <p>${msg("fps.supportHint")}</p>
  </footer>
</div>
</body>
</html>
