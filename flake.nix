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

          # .NET SDK
          pkgs.dotnet-sdk_10

          # Useful dev tools
          pkgs.git
        ];

        # Prevent dotnet from polluting the home directory
        DOTNET_CLI_TELEMETRY_OPTOUT = "1";
        DOTNET_NOLOGO = "1";

        shellHook = ''
          # Store NuGet packages in the shared XDG cache dir (~/.cache/nuget/packages)
          # rather than the per-project directory or the default ~/.nuget/packages.
          # Shared across all dotnet projects on this machine and accessible inside
          # nix develop without any sandbox restrictions.
          export NUGET_PACKAGES="$HOME/.cache/nuget/packages"
        '';
      };
    };
}
