<#-- FairSpot branded login page. Keycloak-standard FTL variables. -->
<!DOCTYPE html>
<html lang="${(locale.currentLanguageTag)!'en'}">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="robots" content="noindex, nofollow">
  <title>${msg("loginAccountTitle")}</title>
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

    <h1 class="fairspot-title">${msg("loginAccountTitle")}</h1>

    <#if message?has_content>
      <div class="fairspot-alert fairspot-alert-${message.type}" role="alert" aria-live="assertive">
        ${kcSanitize(message.summary)?no_esc}
      </div>
    </#if>

    <#if realm.password>
      <form id="kc-form-login" action="${url.loginAction}" method="post">

        <#if !usernameHidden?? || !usernameHidden>
          <div class="fairspot-field">
            <label class="fairspot-label" for="username">
              <#if !realm.loginWithEmailAllowed>${msg("username")}<#elseif !realm.registrationEmailAsUsername>${msg("usernameOrEmail")}<#else>${msg("email")}</#if>
            </label>
            <input class="fairspot-input<#if messagesPerField.existsError('username','password')> fairspot-input-error</#if>"
                   type="text"
                   id="username"
                   name="username"
                   value="${login.username!''}"
                   tabindex="1"
                   autocomplete="username"
                   autocapitalize="off"
                   autocorrect="off"
                   spellcheck="false"
                   aria-describedby="<#if messagesPerField.existsError('username')>username-error</#if>"
                   autofocus />
            <#if messagesPerField.existsError('username')>
              <span id="username-error" class="fairspot-field-error" aria-live="polite">
                ${kcSanitize(messagesPerField.get('username'))?no_esc}
              </span>
            </#if>
          </div>
        </#if>

        <div class="fairspot-field">
          <div class="fairspot-label-row">
            <label class="fairspot-label" for="password">${msg("password")}</label>
            <#if realm.resetPasswordAllowed>
              <a class="fairspot-link fairspot-forgot" href="${url.loginResetCredentialsUrl}" tabindex="5">
                ${msg("doForgotPassword")}
              </a>
            </#if>
          </div>
          <input class="fairspot-input<#if messagesPerField.existsError('username','password')> fairspot-input-error</#if>"
                 type="password"
                 id="password"
                 name="password"
                 tabindex="2"
                 autocomplete="current-password"
                 aria-describedby="<#if messagesPerField.existsError('password')>password-error</#if>" />
          <#if messagesPerField.existsError('password') && !messagesPerField.existsError('username')>
            <span id="password-error" class="fairspot-field-error" aria-live="polite">
              ${kcSanitize(messagesPerField.get('password'))?no_esc}
            </span>
          </#if>
        </div>

        <#if realm.rememberMe && !usernameHidden??>
          <div class="fairspot-remember fairspot-field">
            <label class="fairspot-checkbox-label">
              <input class="fairspot-checkbox" type="checkbox" name="rememberMe" tabindex="3"
                <#if login.rememberMe?? && login.rememberMe>checked</#if> />
              ${msg("rememberMe")}
            </label>
          </div>
        </#if>

        <input type="hidden" name="credentialId"
          <#if auth.selectedCredential?has_content>value="${auth.selectedCredential}"</#if> />

        <button class="fairspot-btn" type="submit" tabindex="4">${msg("doLogIn")}</button>

      </form>

      <#if realm.registrationAllowed && !registrationDisabled??>
        <p class="fairspot-register-hint">
          ${msg("noAccount")} <a class="fairspot-link" href="${url.registrationUrl}" tabindex="6">${msg("doRegister")}</a>
        </p>
      </#if>
    </#if>

  </main>

  <footer class="fairspot-footer">
    <p>${msg("fps.supportHint")}</p>
  </footer>
</div>
</body>
</html>
