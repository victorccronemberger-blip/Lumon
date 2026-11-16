# Lumon

Projeto Unity 6000.3.27f1.

## Abrir o projeto

1. Clone o repositório com `git clone https://github.com/victorccronemberger-blip/Lumon.git`.
2. No Unity Hub, instale o Unity Editor **6000.3.27f1** e adicione a pasta clonada como projeto existente. Aguarde a importação inicial.
3. No Editor, escolha **Lumon → Open HubWorld** e entre em Play Mode. Use WASD ou as setas para andar, **E** para abrir a barraca da Equipe e **F** nas outras estações.

O código e os assets de jogo ficam em `Assets/`. As pranchas de arte, scripts de preparação e capturas ficam em `Art/`. Para compilar a versão Windows x64, execute `Lumon.HubWorld.Editor.HubReleaseBuilder.InstallAndBuild` no Editor em modo batch. A saída fica em `Builds/HubWorld-Win64/`. Veja o [guia do HubWorld](Docs/HubWorld.md) para a sequência de verificação.

A cena do [HubWorld do Observatório](Docs/HubWorld.md) está disponível em **Lumon → Open HubWorld**. O guia reúne as camadas animadas e os testes que faltam para fechar a praça.

A direção atual da Guardiã é [arte 2D pintada](Docs/Guardiao2D.md). Abra **Lumon → Open 2D Guardian Review** para revisar o sprite na praça ilustrada. Em Play Mode, WASD e setas movem a Guardiã e trocam sua orientação entre oito poses sob a câmera fixa.

O [fechamento de R0 e R1](Docs/UX-Crawler-Package/R0-R1-Fechamento.md) acompanha os gates da candidata. A [prova de volume anterior](Docs/Guardiao.md) registra o estudo 3D.

O [protocolo de entrega](Docs/Protocolo-de-Entrega.md) define as responsabilidades de Codex, Higgsfield e Claude Code e a evidência necessária para fechar cada tarefa.
