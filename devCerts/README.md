# Use local azurite emulator to test when using Containerized instance of Orleans silo

When you use local azurite emulator as the Orleans Grain Storage & clustering to test, you may encounter following error messages when running local docker/podman container in debugger session:
```
AuthenticationException: The remote certificate is invalid according to the validation procedure: RemoteCertificateNameMismatch
AuthenticationException: The remote certificate is invalid according to the validation procedure: RemoteCertificateNameMismatch, RemoteCertificateChainErrors
AuthenticationException: The remote certificate is invalid because of errors in the certificate chain: PartialChain
```

That is because the https certificates generated for azurite by the [**mkcert**](https://github.com/FiloSottile/mkcert) development certificates generate tool, its Root CA certificate is trust only on your dev machine(which has invoked the `mkert install`), but not trusted by those running in container instance, so you need to trust the Root CA certificate in the container, you can deal with following steps:

1. Copy the Root CA certificate file from your dev machine to this folder, you can use following command to show the folder that contains the Root CA certificate file of your dev machine:  
    ```sh
    mkcert -CAROOT
    ```
    And the **rootCA.pem** is the file you need to copy to this folder.  
2. Build the debug version of the container image, which is the build stage name postfix like "-mkcert-dev" of multi-stage builds of dockerfile, which I've facilitated the process using `--build-arg` to specify, like this:  
    ```sh
    docker build -f [TheDockerfile_location] --build-arg imgVer="-mkcert-dev" -t [TheImageNameAndTag] .
    ```

When running the docker-compose project that resides on the root folder of this repo, be sure to generate azurite emulator certificates using following command:
```
mkcert table.internal.host
```
and copy the both cert and key *.pem* files to this folder.

Do not check-in any **.pem** files inside this folder, it is ignored by git.
