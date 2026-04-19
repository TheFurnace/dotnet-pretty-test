{
  description = "dotnet-pretty-test dev environment";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
  };

  outputs = { self, nixpkgs, ... }:
    let
      system = "x86_64-linux";
      pkgs   = nixpkgs.legacyPackages.${system};
    in
    {
      devShells.${system}.default = pkgs.mkShell {
        packages = [

          # .NET SDK
          pkgs.dotnet-sdk_10

          # Useful dev tools
          pkgs.git
        ];

        # Prevent dotnet from polluting the home directory
        DOTNET_CLI_TELEMETRY_OPTOUT = "1";
        DOTNET_NOLOGO = "1";
      };
    };
}
