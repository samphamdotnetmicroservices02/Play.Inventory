# Play.Inventory.Contracts
Common library used by Play Economy services.

## Create and publish package
```powershell
$version="1.0.12"
$owner="samphamdotnetmicroservices02"
$gh_pat="[PAT HERE]"

dotnet pack src\Play.Inventory.Contracts\ --configuration Release -p:PackageVersion=$version -p:RepositoryUrl=https://github.com/$owner/Play.Inventory -o ../packages

dotnet nuget push ..\packages\Play.Inventory.Contracts.$version.nupkg --api-key $gh_pat --source "github"

--source "github" comes from Play.Infra
```

```zsh
version="1.0.12"
owner="samphamdotnetmicroservices02"
gh_pat="[PAT HERE]"

dotnet pack src/Play.Inventory.Contracts/ --configuration Release -p:PackageVersion=$version -p:RepositoryUrl=https://github.com/$owner/Play.Inventory -o ../packages

dotnet nuget push ../packages/Play.Inventory.Contracts.$version.nupkg --api-key $gh_pat --source "github"

--source "github" comes from Play.Infra
```

## Build the Docker image
```powershell
$env:GH_OWNER="samphamdotnetmicroservices02"
$env:GH_PAT="[PAT HERE]"
$acrName="samphamplayeconomyacr"

docker build --secret id=GH_OWNER --secret id=GH_PAT -t play.inventory:$version .

or

docker build --secret id=GH_OWNER --secret id=GH_PAT -t "$acrName.azurecr.io/play.inventory:$version" .

-t is tack, tack is really a human friendly way to identify your Docker image, in this case, in your 
box. And it is composed of two parts. The first part is going to be kind of the name of image, and
the second part is the version that you want to assign to it.
the "." next to $version is the cecil file , the context for this docker build command, which in this case
is just going to be ".", this "." represents the current directory
```

```zsh
export GH_OWNER="samphamdotnetmicroservices02"
export GH_PAT="[PAT HERE]"
acrName="samphamplayeconomyacr"

docker build --secret id=GH_OWNER --secret id=GH_PAT -t play.inventory:$version .

or

docker build --secret id=GH_OWNER --secret id=GH_PAT -t "$acrName.azurecr.io/play.inventory:$version" .

check this link for more details about env variable on mac
https://phoenixnap.com/kb/set-environment-variable-mac
```

```
verify your image
docker images
```

## Run the docker image
```powershell
$cosmosDbConnString="[CONN STRING HERE]"
$serviceBusConnString="[CONN STRING HERE]"

docker run -it --rm -p 5004:5004 --name inventory -e MongoDbSettings__Host=mongo -e RabbitMqSettings__Host=rabbitmq --network playinfra_default play.inventory:$version

if you do not use MongoDb and RabbitMQ from Play.Infra, you can remove --network playinfra_default

docker run -it --rm -p 5004:5004 --name inventory -e MongoDbSettings__ConnectionString=$cosmosDbConnString -e ServiceBusSetting__ConnectionString=$serviceBusConnString -e ServiceSettings__MessageBroker="SERVICEBUS" play.inventory:$version

-it: what it does is it creates and kind of an interactive shell, so that you will not able to go back to your
command line until you cancel the execution of these docker run command.

--rm: means that the docker container that is going to be created has to be destroyed as soon as you exit
the execution of these docker run command. That is just to keep things clean in your box.

-p: -p 5004:5004 it means 5004 on the right side is the port of docker container, the left side is port of your
local machine

--name: this is optional

-e: this is environment variable. MongoDbSettings__: MongoDbSettings comes from appsettings.json, the double underscore "__" allows you to specify envinroment variables that will end up in configuration with the same shape that we have, this is the file in the appsettings.json. "MongoDbSettings__Host" Host represents the property in MongoDbSettings in appsettings.json, this will override whatever configuration you specified in appsettings.json
"MongoDbSettings__Host=mongo" mongo is the container_name that we name it in Play.Infra

--network playinfra_default: run "docker network ls" to check your playinfra network. "playinfra_default" comes from
this network. This is a network that has been created by docker compose for everything that we declared in this
docker compose file. So all the containers running in or declared in docker compose (Play.Infra) are running 
within a single network, docker network. And that is how they can reach out to each other easily. However our
microservice is going to be running externally to this. They are not going to be running within these docker
compose file.
So how can they reach out to containers are running in a different network? So we have to find a network for
the docker run command. So we have to add parameter that specifies that we want to connect to the same network
where all the other containers are running "playinfra_default (RabbitMq and Mongo)"

And lastly we have to specify the docker image that we want to run (play.inventory:$version)
```

```zsh
cosmosDbConnString="[CONN STRING HERE]"
serviceBusConnString="[CONN STRING HERE]"
docker run -it --rm -p 5004:5004 --name inventory -e MongoDbSettings__Host=mongo -e RabbitMqSettings__Host=rabbitmq --network playinfra_default play.inventory:$version
```

## Publish the image
```powershell
$acrName="samphamplayeconomyacr"

az acr login --name $acrName

docker tag play.inventory:$version "$acrName.azurecr.io/play.inventory:$version"

docker images (check your images for ACR)

docker push "$acrName.azurecr.io/play.inventory:$version" (go to your ACR -> Repositories to check your images)


az acr login: in order to be able to publish anything into ACR, you will have to first log in into it. Because remember that an ACR is a private repository.
So people cannot just connect to it from anywhere without providing credentials. It is not a repository like it will be the case in Docker Hub.
This is private, so you need credentials to be able to access it. So to do that, what you can do is use the AZ ACR login command from Azure CLI

docker tag play.inventory:$version: The next thing we want to do is a retagging of your image, so that it is ready to be published to ACR. In order to be
able to publish these to ACR, you have to have the name of the repository of your image, has to match a combination of the login server of your ACR
and the accurate repository name (samphamplayeconomyacr.azurecr.io/play.inventory:$version, samphamplayeconomyacr.azurecr.io comes from your login server of ACR)

docker push: publishing image
```

```zsh
acrName="samphamplayeconomyacr"

az acr login --name $acrName

docker tag play.inventory:$version "$acrName.azurecr.io/play.inventory:$version"

docker images (check your images for ACR)

docker push "$acrName.azurecr.io/play.inventory:$version" (go to your ACR -> Repositories to check your images)


if you do not use MongoDb and RabbitMQ from Play.Infra, you can remove --network playinfra_default

docker run -it --rm -p 5004:5004 --name inventory -e MongoDbSettings__ConnectionString=$cosmosDbConnString -e ServiceBusSetting__ConnectionString=$serviceBusConnString -e ServiceSettings__MessageBroker="SERVICEBUS" play.inventory:$version
```

## Creating the Kubernetes namespace
```powershell
$namespace="inventory"
kubectl create namespace $namespace

namespace: the namespace is nothing more than a way to separate the resources that belong to different applications in your Kubernetes cluster. So usallly 
you will have one namespace paired microservice in this case, we will put all the resources that belong to that specific microservice.
```

```zsh
namespace="inventory"
kubectl create namespace $namespace
```


## Creating the Kubernetes pod
```powershell
kubectl apply -f ./kubernetes/inventory.yaml -n $namespace

kubectl get pods -n $namespace
kubectl get pods -n $namespace -w
READY 1/1: means that we have one container inside the pod, and that one pod is ready
AGE: is the time your pod run from the past to the current time
-w: listen to it until a new version deployment is alive.

kubectl get services -n $namespace
TYPE: ClusterIP is the default type, which is ClusterIP meaning that it gets an IP that is local to the cluster
(CLUSTER-IP), so only any other ports within the cluster can reachout these microservice right now. And it is
listening in port 80. External-IP needs to define "spec.type: LoadBalancer" in yaml file


kubectl logs catalog-deployment-5767558688-p9zh2 -n $namespace
catalog-deployment-5767558688-p9zh2: is the name when you run "kubectl get pods -n $namespace"

kubectl logs ...: You want to know what is going on with that pod, what is happening inside that pod

kubectl describe pod catalog-deployment-5767558688-p9zh2 -n $namespace

describe pod: this will give you even more insights into definition of the pod.
```

## Creating the Azure Managed Identity and grating it access to Key Vault secrets
from https://learn.microsoft.com/en-gb/azure/aks/workload-identity-deploy-cluster

```powershell
$appname="playeconomy"
$keyVaultName="samphamplayeconomykv"
$namespace="inventory"

az identity create --resource-group $appname --name $namespace

$IDENTITY_CLIENT_ID=az identity show -g $appname -n $namespace --query clientId -otsv

az keyvault set-policy -n $keyVaultName --secret-permissions get list --spn $IDENTITY_CLIENT_ID

if you receive an error like "AADSTS530003: Your device is required to be managed to access this resource."
after running "az keyvault set-policy ...", try to run it on Cloud

check your Azure Managed Identity by navigate to resource group -> $keyVaultName -> AccessPolicies to see the $namespace have permission Get, List. And you can see
the $namespace in resource group -> $namespace

az identity create: create one of Azure managed identities
--name: the name of the managed identity.

after run "az identity create ...", we have the "clientId" from the response. What we want to do is to retrieve what is known as the identity clientId,
which we are gonna be using to assign permissions into our key vault.

az identity show: is a command to retrieve details about a managed identity that we have already created. 

-n from "az identity show -g $appname -n $namespace" is the name of identity

--query clientId: we want to say that we do not want to just query all the details about this identity, we want to the specific property of that identity. So we
will say query and retrive the clientId

-otsv: we want the "--query clientId" in a format otsv that is easy to parse for other commands.

az keyvault set-policy: after run "$IDENTITY_CLIENT_ID=az identity ...", use the clientId to grant access to our key vault secrets or to our Azure key vault
-n: the name of key vault
--secret-persmissions get list --spn $IDENTITY_CLIENT_ID: "--secret-persmissions" states that we are going to be grarting permissions into our key vault secrets. 
It could be cetificates, it could be keys or it could be secrets. In this case it is going to be just secrets. And the permission we want to grant is "get list".
"--spn $IDENTITY_CLIENT_ID" And then the identity or the service principle that we want to grant these permissions into, is going to be our identity clientId
```

```zsh
appname="playeconomy"
keyVaultName="samphamplayeconomykv"
namespace="inventory"

az identity create --resource-group $appname --name $namespace

export IDENTITY_CLIENT_ID="$(az identity show -g $appname -n $namespace --query clientId -otsv)"

az keyvault set-policy -n $keyVaultName --secret-permissions get list --spn $IDENTITY_CLIENT_ID
```

## Establish the federated identity credential
```powershell
$aksName="samphamplayeconomyaks"

$AKS_OIDC_ISSUER=az aks show -n $aksName -g $appname --query "oidcIssuerProfile.issuerUrl" -otsv

az identity federated-credential create --name $namespace --identity-name $namespace --resource-group $appname --issuer $AKS_OIDC_ISSUER --subject "system:serviceaccount:${namespace}:${namespace}-serviceaccount" --audience api://AzureADTokenExchange

check your result: navigate your resource group $namespace, in this case is identity (Managed Identity) -> Federated credentials tab

retrieve this oidcIssuerProfile.issuerUrl: the only reason why we are able to query this is because when we created the cluster, if you remember we asked it
to enable these OIDC issuer at cluster creation time. So you have to do it that way, otherwise it will not work.

az identity federated-credential... --name: the name of our managed identity. So that name in our case is namespace which in my case is "identity". Because it is
the identity microservice. This is the name of the federated credential

az identity federated-credential... --identity-name: the name of managed identity, the identity name. So for that, we are going to be putting again, namespace.
This is the name of the managed identity we already created before

az identity federated-credential... --subject: your service account that you just created. 
"system:serviceaccount:${namespace}:${namespace}-serviceaccount", the first $namespace, general case is just identity, and the second $namespace is the actual name of the service account which lives in kubernetes/identity.yaml and the ${namespace}-serviceaccount lives in $namespace (identity)
```

```zsh
aksName="samphamplayeconomyaks"
export AKS_OIDC_ISSUER="$(az aks show -n $aksName -g "${appname}" --query "oidcIssuerProfile.issuerUrl" -otsv)"

az identity federated-credential create --name $namespace --identity-name $namespace --resource-group $appname --issuer $AKS_OIDC_ISSUER --subject "system:serviceaccount:${namespace}:${namespace}-serviceaccount" --audience api://AzureADTokenExchange
```

## Delete Kubernetes resources if using Helm chart
Because we deploy our service to Kubernetes using kubectl, So the first thing is to delete one by one

```
kubectl delete deployment inventory-deployment -n $namespace
kubectl delete service inventory-service -n $namespace
kubectl delete serviceaccount inventory-serviceaccount -n $namespace

kubectl get all -n $namespace (verify you delete all resources)
```

## Install the helm chart
```powershell
$acrName="samphamplayeconomyacr"
$helmUser=[guid]::Empty.Guid (or helmUser=00000000-0000-0000-0000-000000000000)
$helmPassword=az acr login --name $acrName --expose-token --output tsv --query accessToken
$chartVersion="0.1.2"

helm registry login "$acrName.azurecr.io" --username $helmUser --password $helmPassword (login to ACR)

helm upgrade inventory-service oci://$acrName.azurecr.io/helm/microservice --version $chartVersion -f ./helm/values.yaml -n $namespace --install
helm upgrade inventory-service oci://$acrName.azurecr.io/helm/microservice --version $chartVersion -f ./helm/values.yaml -n $namespace --install --debug
or 
helm upgrade inventory-service ./helm -f ./helm/values.yaml -n $namespace --install

helm list -n $namespace
helm repo update
```
- helm install indentity-service: "identity-service" is the name you want, this is the name of your release
- ./helm: the location where you have your chart, which is your helm directory
- -f ./helm/values.yaml: the value of your helm. Remember that values file is going to override all of the placeholders
that we have defined directly into the template
- after run the command above, we will see the result. the "REVISION" from the result is very insteresting because "REVISION"
is going to keep track of any subsequent installations of this chart for your microservice in the future. So next time you
run an installation of your microservice via this chart, it will say revision 2, and the revision 3, 4, and so on. And thanks
to that, you will be able to roll back later on into a previous revision if something is just going wrong with the latest
version of your microservice. So it's super interesting.
- helm list -n $namespace: get a list of the installed charts at this point
- $helmUser ...: Because we're going to acr, so we don't need to specify which username here. But we need to follow the
convention, so we put Guid.Empty in the username.
- $helmPassword ...: get password to login acr, --output tsv: the format from the output "--expose-token" is not approiate 
to be used as the argument for the next line. So let's actually modify the output a little bit by using the "--output tsv"
argument. So that it will give you a string that we can use in the next command.
- --query accessToken: accessToken is one component of that output. So we only get only that piece as a string that we can 
user later on
- helm upgrade ... --install: the very first time if you don't have helm chart, it will install, the next time is to upgrade
the helm chart version.
- "oci://$acrName.azurecr.io/helm/microservice" is where you push your helm chart to ACR. "--version $chartVersion" is the
version of your helm chart inside helm/microservice
- "helm upgrad ... --install --debug": give you more information if upgrading service failed
- "helm repo update": to make sure that all of your local cache charts are up to date.

```zsh
acrName="samphamplayeconomyacr"
helmUser=00000000-0000-0000-0000-000000000000
export helmPassword="$(az acr login --name $acrName --expose-token --output tsv --query accessToken)"
chartVersion="0.1.2"

helm registry login "$acrName.azurecr.io" --username $helmUser --password $helmPassword (login to ACR)

helm upgrade inventory-service oci://$acrName.azurecr.io/helm/microservice --version $chartVersion -f ./helm/values.yaml -n $namespace --install
helm upgrade inventory-service oci://$acrName.azurecr.io/helm/microservice --version $chartVersion -f ./helm/values.yaml -n $namespace --install --debug
or 
helm upgrade inventory-service ./helm -f ./helm/values.yaml -n $namespace --install

helm list -n $namespace
helm delete <release-name> -n $namespace
helm repo update
```

## Rollback the previous version using helm
https://helm.sh/docs/helm/helm_rollback/

```powershell
helm history inventory-service -n $namespace (check your all revision numbers)
helm rollback inventory-service -n $namespace (if you dont specify the revision number here, it will roll back to previous number)
or 
helm rollback inventory-service [REVISION] -n $namespace
```

When you roll back, helm will also increase your a number for revision

## Required repository secrets for Github workflow
GH_PAT: Created in Github user profile --> Settings --> Developer settings --> Personal access token
AZURE_CLIENT_ID: From AAD App Registration
AZURE_SUBSCRIPTION_ID: From Azure Portal subscription
AZURE_TENANT_ID: From AAD properties page 
