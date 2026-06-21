<#-- FairSpot branded login page. Keycloak-standard FTL variables. -->
<!DOCTYPE html>
<html lang="${locale.currentLanguageTag!'en'}">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="robots" content="noindex, nofollow">
  <title>${msg("loginAccountTitle")}</title>
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

    <h1 class="fps-title">${msg("loginAccountTitle")}</h1>

    <#if message?has_content>
      <div class="fps-alert fps-alert-${message.type}" role="alert" aria-live="assertive">
        ${kcSanitize(message.summary)?no_esc}
      </div>
    </#if>

    <#if realm.password>
      <form id="kc-form-login" action="${url.loginAction}" method="post">

        <#if !usernameHidden?? || !usernameHidden>
          <div class="fps-field">
            <label class="fps-label" for="username">
              <#if !realm.loginWithEmailAllowed>${msg("username")}<#elseif !realm.registrationEmailAsUsername>${msg("usernameOrEmail")}<#else>${msg("email")}</#if>
            </label>
            <input class="fps-input<#if messagesPerField.existsError('username','password')> fps-input-error</#if>"
                   type="text"
                   id="username"
                   name="username"
                   value="${(login.username!'')?html}"
                   tabindex="1"
                   autocomplete="username"
                   autocapitalize="off"
                   autocorrect="off"
                   spellcheck="false"
                   aria-describedby="<#if messagesPerField.existsError('username')>username-error</#if>"
                   autofocus />
            <#if messagesPerField.existsError('username')>
              <span id="username-error" class="fps-field-error" aria-live="polite">
                ${kcSanitize(messagesPerField.get('username'))?no_esc}
              </span>
            </#if>
          </div>
        </#if>

        <div class="fps-field">
          <div class="fps-label-row">
            <label class="fps-label" for="password">${msg("password")}</label>
            <#if realm.resetPasswordAllowed>
              <a class="fps-link fps-forgot" href="${url.loginResetCredentialsUrl}" tabindex="5">
                ${msg("doForgotPassword")}
              </a>
            </#if>
          </div>
          <input class="fps-input<#if messagesPerField.existsError('username','password')> fps-input-error</#if>"
                 type="password"
                 id="password"
                 name="password"
                 tabindex="2"
                 autocomplete="current-password"
                 aria-describedby="<#if messagesPerField.existsError('password')>password-error</#if>" />
          <#if messagesPerField.existsError('password') && !messagesPerField.existsError('username')>
            <span id="password-error" class="fps-field-error" aria-live="polite">
              ${kcSanitize(messagesPerField.get('password'))?no_esc}
            </span>
          </#if>
        </div>

        <#if realm.rememberMe && !usernameHidden??>
          <div class="fps-remember fps-field">
            <label class="fps-checkbox-label">
              <input class="fps-checkbox" type="checkbox" name="rememberMe" tabindex="3"
                <#if login.rememberMe?? && login.rememberMe>checked</#if> />
              ${msg("rememberMe")}
            </label>
          </div>
        </#if>

        <input type="hidden" name="credentialId"
          <#if auth.selectedCredential?has_content>value="${auth.selectedCredential}"</#if> />

        <button class="fps-btn" type="submit" tabindex="4">${msg("doLogIn")}</button>

      </form>

      <#if realm.registrationAllowed && !registrationDisabled??>
        <p class="fps-register-hint">
          ${msg("noAccount")} <a class="fps-link" href="${url.registrationUrl}" tabindex="6">${msg("doRegister")}</a>
        </p>
      </#if>
    </#if>

  </main>

  <footer class="fps-footer">
    <p>${msg("fps.supportHint")}</p>
  </footer>
</div>
</body>
</html>
