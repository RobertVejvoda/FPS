<#-- FairSpot: update-password / set-new-password page -->
<!DOCTYPE html>
<html lang="${locale.currentLanguageTag!'en'}">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="robots" content="noindex, nofollow">
  <title>${msg("updatePasswordTitle")}</title>
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

    <h1 class="fps-title">${msg("updatePasswordTitle")}</h1>

    <#if message?has_content>
      <div class="fps-alert fps-alert-${message.type}" role="alert" aria-live="assertive">
        ${kcSanitize(message.summary)?no_esc}
      </div>
    </#if>

    <form id="kc-passwd-update-form" action="${url.loginAction}" method="post">

      <#-- Hidden username for accessibility / password managers -->
      <#if auth.showUsername??>
        <input type="hidden" id="username" name="username" value="${(login.username!'')?html}"
               autocomplete="username" readonly />
      </#if>

      <div class="fps-field">
        <label class="fps-label" for="password-new">${msg("passwordNew")}</label>
        <input class="fps-input<#if messagesPerField.existsError('password-new','password-confirm')> fps-input-error</#if>"
               type="password"
               id="password-new"
               name="password-new"
               tabindex="1"
               autocomplete="new-password"
               autofocus />
        <#if messagesPerField.existsError('password-new')>
          <span class="fps-field-error" aria-live="polite">
            ${kcSanitize(messagesPerField.get('password-new'))?no_esc}
          </span>
        </#if>
      </div>

      <div class="fps-field">
        <label class="fps-label" for="password-confirm">${msg("passwordConfirm")}</label>
        <input class="fps-input<#if messagesPerField.existsError('password-confirm')> fps-input-error</#if>"
               type="password"
               id="password-confirm"
               name="password-confirm"
               tabindex="2"
               autocomplete="new-password" />
        <#if messagesPerField.existsError('password-confirm')>
          <span class="fps-field-error" aria-live="polite">
            ${kcSanitize(messagesPerField.get('password-confirm'))?no_esc}
          </span>
        </#if>
      </div>

      <input type="hidden" name="stateChecker" value="${stateChecker}" />

      <button class="fps-btn" type="submit" tabindex="3">${msg("doSubmit")}</button>

    </form>

  </main>

  <footer class="fps-footer">
    <p>${msg("fps.supportHint")}</p>
  </footer>
</div>
</body>
</html>
