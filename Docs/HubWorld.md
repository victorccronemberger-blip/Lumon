# Observatório da Aurora · HubWorld

> Aceite visual registrado em 16/11/2026. A [ficha da revisão](Entregas/HubWorld-Aceite-Visual.md) reúne a build aprovada e as evidências.

A build Windows x64 entra direto na praça. Abra [Lumon.exe](../Builds/HubWorld-Win64/Lumon.exe) na pasta da build ou use **Lumon → Open HubWorld** no Unity 6000.3.27f1. A circulação usa WASD ou as setas. E abre a barraca da Equipe quando a Guardiã se aproxima. F aciona a mesa ou a porta próximas. Esc abre configurações ou volta à praça. Os atalhos de equipe, atlas e configurações aceitam clique e Tab.

## Ambiente e circulação

A pintura de referência mede 1672 × 941 px. O Higgsfield repintou as regiões cobertas nos jobs `bcf4c588-d693-4550-905e-b4fa8266d77d`, `b237dfba-df9f-4deb-8756-ae81362db91b` e `da100a82-0edd-4438-96c4-c465b66fabb6`. A preparação alinha as placas, recorta os objetos e limita os reparos aos seus contornos. As capturas usam a câmera fixa da praça e a Guardiã aprendiz atual.

| Elemento | Comportamento |
| --- | --- |
| Árvore da praça e árvore de moldura | Copas respondem a rajadas com ritmos e amplitudes próprios. Os troncos permanecem fixos. O papel do título cobre a copa de moldura na ordem da composição. |
| Três bandeiras | O tecido ondula abaixo da fixação na torre, no poste e na mesa. |
| Três lanternas | As lanternas suspensas balançam pelo suporte, com os halos acompanhando o giro. A luz da mesa pulsa com o corpo fixo. |
| Fogueira | Pedra, lenha, chama e brilho têm funções separadas. Brasas sobem ao ritmo da chama e acompanham sua oclusão. A Guardiã contorna o fogo e passa na frente ou atrás dele conforme a posição dos pés. |
| Praça | Os limites seguem o chão pintado. Barraca, mesa e porta têm áreas de aproximação, prompt de interação e zona de clique. |

A cena usa **15 sprites separados** além da base. As áreas de circulação e interação foram implementadas por Claude Code Fable 5.2. Cada sprite tem posição, pivô, ordem, amplitude, fase e período registrados no manifesto. Consulte a [captura da praça](../Art/HubWorld/review/player-01-praca-pt.png), a [animação no executável](../Art/HubWorld/review/player-ambiente.gif), a [base recomposta](../Art/HubWorld/review/unity-1280x720-base-only.png) e a [evidência de movimento](../Art/HubWorld/review/motion-pixels.json).

## Interface e estado

A [direção de interface](HubWorld-Design.md) documenta cor, tipografia e alinhamento sobre a arte. A barraca da Equipe oferece seleção da equipe, fusão, aprimoramento, compra e incubação de ovos. O saldo da praça acompanha as transações. [Funções da barraca](Entregas/UX-003-Barraca-da-Equipe.md). O atlas permite marcar e remover a rota do Vale da Nascente. As configurações permitem escolher PT-BR, EN ou ES, alterar volume e reduzir movimento. A prévia é revertida ao cancelar. As opções confirmadas, a equipe e a rota são gravadas. 

- [Barraca da Equipe com arte do Higgsfield](../Art/HubWorld/review/design/design-pt-team.png) e [atlas em português](../Art/HubWorld/review/player-03-atlas-pt.png).
- [Configurações em inglês](../Art/HubWorld/review/player-05-settings-en.png) e [configurações em espanhol](../Art/HubWorld/review/player-06-ajustes-es.png).
- [Praça em 1080p após reabrir em espanhol](../Art/HubWorld/review/player-07-praca-es-reabertura.png) e [atlas espanhol em 1440×900](../Art/HubWorld/review/player-09-atlas-es-16x10.png).

## Checklist de fechamento do Hub

- [x] Separar, recompor e animar árvores de moldura e da praça, três bandeiras, três lanternas e fogueira. Confirmar ausência de alterações fora das regiões de movimento nos quadros amostrados. [Validação gráfica](../Art/HubWorld/review/evidence.json)
- [x] Confirmar câmera, spawn, limites de circulação, colisões, aproximação às três estações e profundidade na passagem pela fogueira. [Validação espacial](../Art/HubWorld/review/traversal.json)
- [x] Integrar HUD, equipe, livro e configurações em PT-BR, EN e ES. Revisar hierarquia, encaixe dos textos, seleção e retorno. [Teste no player](../Art/HubWorld/review/player-smoke.txt)
- [x] Exercitar movimento, E na barraca, Esc, Tab, Enter e clique no executável por eventos de entrada do Windows. [Teste nativo](../Art/HubWorld/review/player-input.txt)
- [x] Exercitar as transações da barraca, a eclosão, o movimento reduzido e a reabertura do perfil. [Validação da barraca](Entregas/UX-003-Barraca-da-Equipe.md)
- [x] Confirmar persistência de idioma, equipe, rota, volume e redução de movimento após fechar o processo e executar novamente. [Teste de reabertura](../Art/HubWorld/review/player-reload.txt)
- [x] Gerar Windows x64 e verificar 1280×720, 1920×1080 e proporção 16:10 em 1440×900. [Teste em 16:10](../Art/HubWorld/review/player-wide.txt)
- [x] Integrar brisa, pássaros e som de fogueira com ganho por distância. O player iniciou os dois loops e passou nos testes de volume e aproximação. [Medição de áudio](../Art/HubWorld/review/player-audio.txt)
- [x] Verificar a saída de áudio do executável. A revisão visual mediu os dois loops, silêncio com o volume zerado e retorno do sinal. A gravação WASAPI de referência pertence à candidata anterior. [Medição atual](../Art/HubWorld/review/player-audio.txt) · [Manifesto](../Art/HubWorld/review/release.json)

## Reprodução

1. Execute `python Art/HubWorld/prepare_hub_layers.py` para reconstruir os recortes e `python Art/HubWorld/render_hub_audio.py` para recriar os dois loops.
2. Rode `Lumon.HubWorld.Editor.HubWorldBuilder.BuildAndCapture` no Unity em batch mode com D3D11. Execute `python Art/HubWorld/review_hub_layers.py` para conferir a composição. Para a sequência de movimento, rode `Lumon.HubWorld.Editor.HubWorldMotionPreview.Capture` e `python Art/HubWorld/compose_hub_motion.py`.
3. Rode `Lumon.HubWorld.Editor.HubTraversalSetup.InstallAndVerify` para validar os trajetos. Rode `Lumon.HubWorld.Editor.HubReleaseBuilder.InstallAndBuild` para atualizar a cena e compilar em `Builds/HubWorld-Win64`.
4. Execute `Art/HubWorld/review_hub_player.ps1` para testar a build em três resoluções e `python Art/HubWorld/review_hub_input.py` para verificar a entrada do Windows. Os dois testes usam uma chave de preferências de QA separada.

5. Para repetir a captura de saída, abra o executável normalmente com áudio ativo. Execute o exemplo WASAPI da Microsoft como `ApplicationLoopback <PID do Lumon> includetree <arquivo.wav>`. Durante a gravação, caminhe da rosa dos ventos à região da fogueira. [Exemplo oficial da Microsoft](https://learn.microsoft.com/en-us/samples/microsoft/windows-classic-samples/applicationloopbackaudio-sample/)
