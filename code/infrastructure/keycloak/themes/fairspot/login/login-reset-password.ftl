<#-- FairSpot: forgot-password / reset-password page -->
<!DOCTYPE html>
<html lang="${(locale.currentLanguageTag)!'en'}">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="robots" content="noindex, nofollow">
  <title>${msg("emailForgotTitle")}</title>
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

    <h1 class="fairspot-title">${msg("emailForgotTitle")}</h1>

    <#if message?has_content>
      <div class="fairspot-alert fairspot-alert-${message.type}" role="alert" aria-live="assertive">
        ${kcSanitize(message.summary)?no_esc}
      </div>
    </#if>

    <p style="font-size:14px; color:#6b7280; margin-bottom:20px;">${msg("emailInstruction")}</p>

    <form id="kc-reset-password-form" action="${url.loginAction}" method="post">

      <div class="fairspot-field">
        <label class="fairspot-label" for="username">
          <#if !realm.loginWithEmailAllowed>${msg("username")}<#elseif !realm.registrationEmailAsUsername>${msg("usernameOrEmail")}<#else>${msg("email")}</#if>
        </label>
        <input class="fairspot-input<#if messagesPerField.existsError('username')> fairspot-input-error</#if>"
               type="text"
               id="username"
               name="username"
               value="${auth.attemptedUsername!''}"
               tabindex="1"
               autocomplete="username"
               autocapitalize="off"
               autocorrect="off"
               spellcheck="false"
               autofocus />
        <#if messagesPerField.existsError('username')>
          <span class="fairspot-field-error" aria-live="polite">
            ${kcSanitize(messagesPerField.get('username'))?no_esc}
          </span>
        </#if>
      </div>

      <button class="fairspot-btn" type="submit" tabindex="2">${msg("doSubmit")}</button>

    </form>

    <p style="text-align:center;">
      <a class="fairspot-link fairspot-back-link" href="${url.loginUrl}" tabindex="3">${msg("backToLogin")}</a>
    </p>

  </main>

  <footer class="fairspot-footer">
    <p>${msg("fps.supportHint")}</p>
  </footer>
</div>
</body>
</html>
