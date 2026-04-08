{
  description = "dotnet-pretty-test dev environment";

  inputs = {
    nixpkgs.follows = "nixos/nixpkgs";
    nixos.url       = "git+file:///home/dev/nixos";
    beads.follows = "nixos/beads";
  };

  outputs = { nixpkgs, beads, ... }:
    let
      system = "x86_64-linux";
      pkgs   = nixpkgs.legacyPackages.${system};

      pi = pkgs.writeShellScriptBin "pi" ''
        exec bunx @mariozechner/pi-coding-agent "$@"
      '';

      bd = beads.packages.${system}.default;
    in
    {
      devShells.${system}.default = pkgs.mkShell {
        packages = [
          pkgs.bun
          bd

          # .NET SDK (9 = current stable; bump to dotnet-sdk_10 when available)
          pkgs.dotnet-sdk_9

          # Useful dev tools
          pkgs.git
        ];

        # Prevent dotnet from polluting the home directory
        DOTNET_CLI_TELEMETRY_OPTOUT = "1";
        DOTNET_NOLOGO = "1";
        # Store NuGet packages in the project rather than ~/.nuget
        NUGET_PACKAGES = toString ./. + "/.nuget/packages";
      };
    };
}
