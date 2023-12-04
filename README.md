## 1. Introduction

Provide credential and session management capabilities for Kubernetes clusters.

To use with CyberArk PAM self-hosted or Privilege Cloud.

## 2. CPM Plugins

### 2.1. KubeConfig

#### 2.1.1. Parameters

1. Username
2. Kubernetes Cluster URL
3. Certificate Validity (Days)
4. Random alphanumeric string for initialization

> [!Note]
>
> The method provided by CPM for [returning credentials to CPM](https://docs.cyberark.com/PAS/latest/en/Content/PASIMP/CredentialsGeneratedByTarget.htm) requires the `KeyID` field to be populated
>
> To fulfill this requirement:
> 1. Enter a random string when onboarding the account
> 2. The plugin is configured to populate this field with a random string during change and reconcile actions

#### 2.1.2. Password

##### 2.1.2.1. Onboarding

The plugin works with a **Base-64 encoded** [kubeconfig](https://kubernetes.io/docs/reference/config-api/kubeconfig.v1/) file.

Example:

```sh
echo .kube/config | base64 -w0
```

> [!Note]
>
> The kubeconfig file should only contain 1 user entry
>
> If there are multiple user entries, only the first one will be managed

##### 2.1.2.2. Change

The plugin creates a new user certificate with the following flow:
- Create a RSA-2048 private key-pair
- Create a [CSR](https://kubernetes.io/docs/reference/access-authn-authz/certificate-signing-requests/) and submit to the Kubernetes cluster
- Approve the CSR

> [!Note]
>
> The kubeconfig file needs to have the appropriate permissions to submit and approve CSR for the change operation to succeed
>
> Otherwise, use reconcile method with another kubeconfig account with the permissions

> [!Warning]
>
> Kubernetes does not support certificate revocation
>
> Minimize the chance of certificate abuse by minimizing the certificate validity and adjusting the change period to issue a new certificate for the current one expires

### 2.2. Service Account

#### 2.2.1. Parameters

1. Service Account Name
2. Kubernetes Cluster URL
3. Namespace
4. Token Lifetime (Days)
5. Random alphanumeric string for initialization

> [!Note]
>
> The method provided by CPM for [returning credentials to CPM](https://docs.cyberark.com/PAS/latest/en/Content/PASIMP/CredentialsGeneratedByTarget.htm) requires the `KeyID` field to be populated
>
> To fulfill this requirement:
> 1. Enter a random string when onboarding the account
> 2. The plugin is configured to populate this field with a random string during change and reconcile actions

#### 2.2.2. Password

##### 2.2.2.1. Onboarding

The plugin works with [service account tokens](https://kubernetes.io/docs/reference/access-authn-authz/authentication/#service-account-tokens)

##### 2.2.2.2. Change

[time-limited API token](https://kubernetes.io/docs/tasks/configure-pod-container/configure-service-account/#manually-create-an-api-token-for-a-serviceaccount)

> [!Note]
>
> The service needs to have the appropriate permissions to create tokens for the change operation to succeed
>
> Otherwise, use reconcile method with a kubeconfig account with the permissions

> [!Warning]
>
> Kubernetes does not support revocation of time-limited API token
>
> Minimize the chance of token abuse by minimizing the token lifetime and adjusting the change period to issue a new token for the current one expires

## 3. PSM Connector

To be updated
