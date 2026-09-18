#!/usr/bin/env bash
set -euo pipefail

if [[ "${EUID}" -ne 0 ]]; then
  echo "Ejecuta este script como root dentro de WSL." >&2
  exit 1
fi

node_version="24.21.0"
node_base="/opt/nodejs"
dotnet10_base="/opt/dotnet10"

case "$(uname -m)" in
  x86_64) node_arch="x64" ;;
  aarch64|arm64) node_arch="arm64" ;;
  *) echo "Arquitectura WSL no compatible: $(uname -m)" >&2; exit 1 ;;
esac

echo "==> Instalando requisitos del sistema"
apt-get update
DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
  ca-certificates curl xz-utils

for command_name in node npm npx corepack dotnet10; do
  command_path="/usr/local/bin/${command_name}"
  if [[ -e "${command_path}" && ! -L "${command_path}" ]]; then
    echo "No se sobrescribira ${command_path}: existe y no es un enlace simbolico." >&2
    exit 1
  fi
done

node_directory="${node_base}/node-v${node_version}-linux-${node_arch}"
if [[ ! -x "${node_directory}/bin/node" ]]; then
  echo "==> Instalando Node.js v${node_version} para Linux ${node_arch}"
  bootstrap_tmp="$(mktemp -d /tmp/miswidgets-feed-bootstrap.XXXXXX)"
  trap 'rm -rf "${bootstrap_tmp}"' EXIT

  archive="node-v${node_version}-linux-${node_arch}.tar.xz"
  distribution="https://nodejs.org/dist/v${node_version}"
  curl -fsSLo "${bootstrap_tmp}/${archive}" "${distribution}/${archive}"
  curl -fsSLo "${bootstrap_tmp}/SHASUMS256.txt" "${distribution}/SHASUMS256.txt"
  (
    cd "${bootstrap_tmp}"
    grep "  ${archive}$" SHASUMS256.txt | sha256sum --check --strict -
  )

  install -d -m 0755 "${node_base}"
  tar -xJf "${bootstrap_tmp}/${archive}" -C "${node_base}"
fi

ln -sfn "${node_directory}/bin/node" /usr/local/bin/node
ln -sfn "${node_directory}/bin/npm" /usr/local/bin/npm
ln -sfn "${node_directory}/bin/npx" /usr/local/bin/npx
if [[ -x "${node_directory}/bin/corepack" ]]; then
  ln -sfn "${node_directory}/bin/corepack" /usr/local/bin/corepack
fi

if [[ ! -x "${dotnet10_base}/dotnet" ]] || \
   ! "${dotnet10_base}/dotnet" --list-sdks | grep -q '^10\.'; then
  echo "==> Instalando el SDK .NET 10 aislado"
  bootstrap_tmp="${bootstrap_tmp:-$(mktemp -d /tmp/miswidgets-feed-bootstrap.XXXXXX)}"
  trap 'rm -rf "${bootstrap_tmp}"' EXIT
  curl -fsSLo "${bootstrap_tmp}/dotnet-install.sh" https://dot.net/v1/dotnet-install.sh
  chmod 0755 "${bootstrap_tmp}/dotnet-install.sh"
  "${bootstrap_tmp}/dotnet-install.sh" \
    --channel 10.0 \
    --quality GA \
    --install-dir "${dotnet10_base}" \
    --no-path
fi

ln -sfn "${dotnet10_base}/dotnet" /usr/local/bin/dotnet10

echo "==> Versiones disponibles"
node --version
npm --version
dotnet --version
dotnet10 --version
