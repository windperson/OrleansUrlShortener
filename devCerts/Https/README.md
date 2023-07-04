# Note

This folder is for generating Https certificates on dev-time docker container running, see:

<https://learn.microsoft.com/aspnet/core/security/docker-https#running-pre-built-container-images-with-https>

## How to generate certificates

1. Run `dotnet dev-certs https -ep ./aspnetapp.pfx -p <password>` in this folder to generate the certificate.
2. On Windows or macOS, you can run `dotnet dev-certs https --trust` to trust that certificate.
