#!/usr/bin/env bash
# Первичная настройка чистой Ubuntu под стенд.
# Запускается один раз на свежей машине:
#
#   bash deploy/setup.sh
#
# Ставит Docker, включает подкачку и открывает 80 и 443.

set -euo pipefail

echo "==> обновляю список пакетов"
sudo apt-get update -qq

echo "==> ставлю Docker из официального репозитория"
sudo apt-get install -y -qq ca-certificates curl

sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg \
    -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc

echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] \
https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
    | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt-get update -qq
sudo apt-get install -y -qq \
    docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

echo "==> разрешаю запускать docker без sudo"
sudo usermod -aG docker "$USER"

# Сборка движка .NET требовательна к памяти. На машине с 4 ГБ
# без подкачки она может упасть на середине.
if [ ! -f /swapfile ]; then
    echo "==> добавляю 4 ГБ подкачки"
    sudo fallocate -l 4G /swapfile
    sudo chmod 600 /swapfile
    sudo mkswap /swapfile > /dev/null
    sudo swapon /swapfile
    echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab > /dev/null
fi

if command -v ufw > /dev/null; then
    echo "==> открываю порты 80 и 443"
    sudo ufw allow 80/tcp  > /dev/null || true
    sudo ufw allow 443/tcp > /dev/null || true
fi

echo
echo "Готово. Docker установлен."
echo "Выйдите и зайдите обратно по ssh, чтобы docker заработал без sudo."
