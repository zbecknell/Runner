# Release Signing Setup

This project signs release artifacts in `.github/workflows/release.yml`.
The workflow packages Runner with Velopack, signs Windows artifacts with Azure Artifact Signing, and signs/notarizes macOS artifacts with Apple Developer ID.

## GitHub Secrets

Add these values in GitHub under **Settings > Secrets and variables > Actions > Repository secrets**.

Windows:

```text
AZURE_CLIENT_ID
AZURE_TENANT_ID
AZURE_SUBSCRIPTION_ID
AZURE_TRUSTED_SIGNING_ENDPOINT
AZURE_TRUSTED_SIGNING_ACCOUNT
AZURE_TRUSTED_SIGNING_CERT_PROFILE
```

macOS:

```text
MACOS_CERT_APP_BASE64
MACOS_CERT_INSTALLER_BASE64
MACOS_CERT_PASSWORD
MACOS_SIGN_APP_IDENTITY
MACOS_SIGN_INSTALL_IDENTITY
MACOS_NOTARY_APPLE_ID
MACOS_NOTARY_TEAM_ID
MACOS_NOTARY_PASSWORD
```

## Windows: Azure Artifact Signing

Azure Artifact Signing was previously named Azure Trusted Signing in many docs and examples. The workflow uses Azure OIDC login, so it does not need an Azure client secret.

### 1. Create the signing account

1. In the Azure portal, create or choose a subscription and resource group.
2. Register the `Microsoft.CodeSigning` resource provider if the subscription has not used Artifact Signing before.
3. Create an **Artifact Signing account**.
4. Choose the region intentionally; the signing endpoint secret must match the region of this account.
5. Save the account name as `AZURE_TRUSTED_SIGNING_ACCOUNT`.

### 2. Complete identity validation

1. Open the Artifact Signing account.
2. Go to **Identity validations**.
3. Create a public identity validation for the relevant person or organization.
4. Complete the verification flow and wait for approval.

Microsoft documents that identity validation can take 1 to 20 business days, and longer if additional documentation is requested.

### 3. Create the certificate profile

1. Open the Artifact Signing account.
2. Go to **Certificate profiles**.
3. Create a public trust certificate profile using the approved identity validation.
4. Save the profile name as `AZURE_TRUSTED_SIGNING_CERT_PROFILE`.

### 4. Capture the endpoint

Set `AZURE_TRUSTED_SIGNING_ENDPOINT` to the endpoint for the Artifact Signing account region.

Examples:

```text
https://eus.codesigning.azure.net/
https://wus.codesigning.azure.net/
https://weu.codesigning.azure.net/
```

Use the endpoint shown by Azure for the selected account region rather than guessing from these examples.

### 5. Create the GitHub Actions Azure identity

1. In Microsoft Entra ID, create an app registration for this repository's release workflow.
2. Copy the app registration **Application (client) ID** to `AZURE_CLIENT_ID`.
3. Copy the directory **Tenant ID** to `AZURE_TENANT_ID`.
4. Copy the Azure subscription ID to `AZURE_SUBSCRIPTION_ID`.
5. Create a federated credential for GitHub Actions:
   - Issuer: `https://token.actions.githubusercontent.com`
   - Organization/repository: `zbecknell/Runner`
   - Entity type: tag
   - Subject pattern: `repo:zbecknell/Runner:ref:refs/tags/v*.*.*`
6. Assign the app/service principal only the roles required to use the Artifact Signing account.

The release workflow grants `id-token: write`, then `azure/login@v2` exchanges the GitHub OIDC token for short-lived Azure credentials.

## macOS: Apple Developer ID Signing And Notarization

Apple Developer ID is required for software distributed outside the Mac App Store. The workflow needs both a Developer ID Application certificate and a Developer ID Installer certificate.

### 1. Confirm Apple access

1. Join the Apple Developer Program.
2. Confirm the account can create Developer ID certificates. Apple documents this as an Account Holder capability.
3. Make sure the Apple ID used for notarization has two-factor authentication enabled.

### 2. Create Developer ID certificates

On a Mac:

1. Open **Keychain Access**.
2. Create a certificate signing request through **Certificate Assistant > Request a Certificate From a Certificate Authority**.
3. In the Apple Developer portal, open **Certificates, IDs & Profiles**.
4. Create a **Developer ID Application** certificate using the CSR.
5. Create a **Developer ID Installer** certificate using a CSR.
6. Download both certificates and double-click them to install them into the login keychain.

### 3. Export `.p12` certificate identities

In Keychain Access:

1. Find the installed **Developer ID Application** identity.
2. Expand it and confirm it includes a private key.
3. Export it as a password-protected `.p12` file.
4. Repeat for the **Developer ID Installer** identity.
5. Use the same strong export password for both files unless you also update the workflow to support separate passwords.
6. Store that password as `MACOS_CERT_PASSWORD`.

If `.p12` export is unavailable, the private key is not in that keychain. Export from the Mac/user account that created the CSR, or create a new certificate from that Mac.

### 4. Convert the `.p12` files to GitHub secret values

Run these commands on macOS from the directory containing the exported files:

```bash
base64 -i "Developer ID Application.p12" | pbcopy
```

Paste the clipboard value into `MACOS_CERT_APP_BASE64`.

```bash
base64 -i "Developer ID Installer.p12" | pbcopy
```

Paste the clipboard value into `MACOS_CERT_INSTALLER_BASE64`.

### 5. Capture certificate identity names

Run:

```bash
security find-identity -v -p codesigning
```

Use the full identity strings as secrets:

```text
MACOS_SIGN_APP_IDENTITY=Developer ID Application: Name or Company (TEAMID)
MACOS_SIGN_INSTALL_IDENTITY=Developer ID Installer: Name or Company (TEAMID)
```

### 6. Create notarization credentials

1. Save the Apple ID email used for notarization as `MACOS_NOTARY_APPLE_ID`.
2. Find the Apple Developer Team ID and save it as `MACOS_NOTARY_TEAM_ID`.
3. Generate an app-specific password for the Apple ID and save it as `MACOS_NOTARY_PASSWORD`.

The release workflow stores these credentials in a temporary keychain profile named `runner-notary`, uses it during packaging, and deletes the temporary keychain at the end of the job.

## Validation

After all secrets are configured:

1. Push a test tag:

   ```powershell
   git tag v0.1.0
   git push origin v0.1.0
   ```

2. Watch the **Release** workflow.
3. If Windows signing fails, check the Azure account name, certificate profile name, endpoint, OIDC subject, and role assignment.
4. If macOS signing fails, check that the `.p12` files include private keys and that the identity strings exactly match `security find-identity`.
5. If notarization fails, inspect the notary log from the GitHub Actions output or rerun locally with the same certificate identities and `notarytool` credentials.

## References

- Azure Artifact Signing quickstart: https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart
- GitHub OIDC with Azure: https://docs.github.com/actions/how-tos/security-for-github-actions/security-hardening-your-deployments/configuring-openid-connect-in-azure
- GitHub Actions secrets reference: https://docs.github.com/en/actions/reference/security/secrets
- Apple Developer ID: https://developer.apple.com/developer-id/
- Apple Developer ID certificates: https://developer.apple.com/help/account/create-certificates/create-developer-id-certificates/
- Apple notarization workflow: https://developer.apple.com/documentation/security/customizing-the-notarization-workflow
- Apple notarytool migration note: https://developer.apple.com/documentation/technotes/tn3147-migrating-to-the-latest-notarization-tool
