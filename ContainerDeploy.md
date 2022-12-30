# UrlShortener frontend Azure Deployment

## App Service (Linux) Custom container

### Build deploy Docker Image via Azure Container Registry using Azure CLI

Install [Azure CLI](https://aka.ms/azurecli) command line tool, [do login](https://learn.microsoft.com/cli/azure/authenticate-azure-cli) then run the following command at project git repo root directory to build deploy Docker image:

```bash
az acr build -t urlshortener/frontend:<image_tag_name> -r <acr_service_name> -f src/UrlShortener.Frontend/Dockerfile .
```

Replace `<image_tag_name>` with the tag name you want to use for the image, using "**`latest`**" will auto deploy to Azure App Service Staging slot,
the `<acr_service_name>` with the name of your Azure Container Registry service.

### Add tag without build on Azure Container Registry

If you want to add tag to existing image without doing another build on ACR or docker cli tool, you can use `az acr import` command's trick to add tag to existing image:

```bash
az acr import -n <acr_service_name> -s <acr_service_name>.azurecr.io/urlshortener/frontend:<image_tag> -t urlshortener/frontend:<new_img_tag> --force
```

The `<acr_service_name>` with the name of your Azure Container Registry service,  
Replace `<image_tag>` with the tag name that is already exist on ACR,  
Replace `<new_image_tag>` with the new tag name you want to add to the image.