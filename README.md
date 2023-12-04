## 1. Introduction

Provide credential and session management capabilities for Kubernetes clusters.

For use with CyberArk PAM self-hosted or Privilege Cloud.

Tested with:

|Software|Version|
|---|---|
|CyberArk PAM self-hosted|13.2|
|Kubernetes|1.28|

## 2. CPM Plugins

### 2.1. KubeConfig

<details><summary><h4>2.1.0. Create user and certificate - example commands</h4></summary>

Create user and bind to a role/clusterrole:

```sh
USERNAME=joe.tan
BINDINGNAME=joe.tan-clusterrolebinding
CLUSTERROLE=cluster-admin
kubectl create clusterrolebinding $BINDINGNAME --clusterrole=$CLUSTERROLE --user=$USERNAME
```

Create key-pair and CSR:

```sh
openssl ecparam -name secp384r1 -genkey -out $USERNAME.key
openssl req -new -key $USERNAME.key -subj "/CN=$USERNAME" -out $USERNAME.csr
openssl x509 -req -in $USERNAME.csr -CA /etc/kubernetes/pki/ca.crt -CAkey /etc/kubernetes/pki/ca.key -CAcreateserial -days 10958 -sha256 -out $USERNAME.pem
```

Submit CSR to Kubernetes cluster:

```sh
CSRNAME=joe.tan-csr
kubectl apply -f - <<EOF
apiVersion: certificates.k8s.io/v1
kind: CertificateSigningRequest
metadata:
  name: $CSRNAME
spec:
  request: $CSR
  signerName: kubernetes.io/kube-apiserver-client
  expirationSeconds: $CERTVALIDITY
  usages:
  - client auth
EOF
```

Approve the CSR:

```sh
kubectl certificate approve $CSRNAME
```

</details>

#### 2.1.1. Account fields

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

#### 2.1.2. Account password

##### 2.1.2.1. Onboard

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

<details><summary><h4>2.2.0. Create service account and token - example commands</h4></summary>

```sh
NAMESPACE=kube-system
SERVICEACCOUNTNAME=joe.tan
CLUSTERROLE=cluster-admin
kubectl -n $NAMESPACE create serviceaccount $SERVICEACCOUNTNAME
kubectl -n $NAMESPACE create clusterrolebinding $CLUSTERROLE-binding --clusterrole=$CLUSTERROLE --serviceaccount=$NAMESPACE:$SERVICEACCOUNTNAME
kubectl -n $NAMESPACE create token $SERVICEACCOUNTNAME --duration=24h
```

</details>

#### 2.2.1. Account fields

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

#### 2.2.2. Account password

##### 2.2.2.1. Onboard

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

The PSM connector for kubectl comprises of:
1. The [`PSMkubectlDispatcher`](./PSM-kubectl/PSMkubectlDispatcher.au3)
  - AutoIT script to work with the [PSM universal connector framework](https://docs.cyberark.com/PAS/latest/en/Content/PASIMP/psm_Develop_universal_connector.htm)
  - Dispatcher flow:
    - Retrieve the account values (username/service account name, kubeconfig/service account token, kubernetes cluster URL, kubectl directory)
    - Set the account values as environment variables for the kubectl wrapper
    - Launch the kubectl wrapper
2. The [`kubectlWrapper.`](PSM-kubectl/kubectlWrapper.cs)
  - Restricted shell inteface built using C# to limit user inputs to kubectl commands
  - Wrapper flow:
    - Determine whether the password type is kubeconfig or service account token
    - Generate the kubeconfig file according to the password type
    - Enforce accepted/denied user inputs:
      - Accepted: inputs that starts with `kubectl`
      - Denied: inputs that contains: `&&`, `|`, `<` and `>`

> [!Note]
>
> Using `kubectl apply`:
> 
> 1. `kubectl apply -f -` does not work as Windows doesn't work with the `<< EOF` + `EOF` redirection
> 
> 2. The restricted shell interface prevents file creation/edit on the kubectl connector, to apply a Kubernetes manifest file, use the PSM drive mapping feature to upload the manifest via the drive mapping, and use `kubectl apply -f Z:\<filename>`

> [!Note]
>
> Executables directory:
>
> The `kubectlWrapper.exe` and `kubectl.exe` executables must be put into the same directory on each PSM server
> 
> The directory where the executables are in should be populated in the connection component setting: `ConnectionComponent` / `TargetSettings` / `ClientSpecific` / `kubectlDirectory`
