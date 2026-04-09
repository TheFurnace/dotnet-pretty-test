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
          # XDG-aligned dotnet/NuGet path overrides.
          # DOTNET_CLI_HOME: runtime appends /.dotnet, so this lands at
          #   $XDG_DATA_HOME/.dotnet instead of ~/.dotnet
          # All NuGet cache dirs consolidated under $XDG_CACHE_HOME/nuget/
          export DOTNET_CLI_HOME="''${XDG_DATA_HOME:-$HOME/.local/share}"
          export NUGET_PACKAGES="''${XDG_CACHE_HOME:-$HOME/.cache}/nuget/packages"
          export NUGET_HTTP_CACHE_PATH="''${XDG_CACHE_HOME:-$HOME/.cache}/nuget/http-cache"
          export NUGET_PLUGINS_CACHE_PATH="''${XDG_CACHE_HOME:-$HOME/.cache}/nuget/plugin-cache"
          export BUN_INSTALL_CACHE_DIR="''${XDG_CACHE_HOME:-$HOME/.cache}/bun"
        '';
      };
    };
}
