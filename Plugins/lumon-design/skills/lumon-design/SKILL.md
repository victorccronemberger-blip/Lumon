---
name: lumon-design
description: Criar e revisar arquitetura, estações, personagens, iluminação e interface do Observatório Lumon em 2,5D. Usar a direção pintada do Amanhecer, a forja jogável e capturas comparáveis da cena Unity.
---

# Direção de arte do Observatório Lumon

Trabalhe a partir da raiz do clone de Lumon. A referência de composição é **o painel da esquerda** do [estudo do Amanhecer](../../../../Art/Concepts/Observatorio/tres-direcoes-de-arte.png). A referência de acabamento é a [forja vista dentro do jogo](../../../../QA/Painted-Dawn/tour/shot_04_oficina.png). Na forja, o arco conduz o olhar até o foco quente, a soleira recebe essa luz e o recorte claro do metal separa a cobertura dos tijolos. Reproduza essa relação entre forma, material e iluminação na peça que estiver criando.

## Do pedido à primeira captura

1. Localize a construção na cena `Assets/Lumon/HubWorld/HubWorld.unity`. Registre a vista do caminho de aproximação e uma vista lateral antes de editar. Identifique o que o jogador deve reconhecer primeiro e a parte que explica o uso do objeto. Na banca é a balança junto aos ovos. Na luneta é a objetiva acima do tripé.
2. Construa primeiro o perfil visto à distância de caminhada. Dê espessura à cobertura, apoio aos volumes salientes e profundidade aos vãos. Gire a câmera e confira onde o braço, o pilar ou a borda encontram seu suporte. Preserve a área de aproximação e os caminhos usados pelos seis jogadores.
3. Pinte em coordenadas da peça. Alterne o comprimento das pedras e escureça a argamassa. No cobre, desenhe a sobreposição das chapas e reserve as linhas claras para as nervuras. Na madeira, leve o veio até a junta. No tecido, agrupe a luz em dobras largas. Use a sombra de contato sob o beiral e o pé de apoio. Escolha o detalhe que ainda é legível com a câmera afastada. Compare com as [construções na praça](../../../../QA/Arquitetura-do-Observatorio/pavilhao-e-torre.jpg) e [as estações em uso](../../../../QA/Arquitetura-do-Observatorio/estacoes-antes-e-agora.jpg).
4. Faça a captura depois da mudança com a mesma posição, órbita e resolução. Observe a escala do personagem, os materiais que se confundem e se o fundo compete com a ação. Se o acabamento só funciona ampliado, ajuste a largura da junta, o perfil da peça ou o contraste local antes de aumentar a densidade do mundo. Entregue a comparação e diga qual detalhe mudou a leitura durante o jogo.

## Escolha da fonte editável

| Mudança | Onde trabalhar | O que verificar no jogo |
| --- | --- | --- |
| Pavilhão, torre, arcada, óptica | `Art/Gaussian/dawn-architecture.mjs` e `painted-surface.mjs` | Vista de entrada, perfil e reverso. Vão aberto entre pilares, recorte da cúpula e continuidade da borda durante a órbita. |
| Sede, banca, ninho, fusão, expedições | `Art/Gaussian/observatory.mjs` e `station-pigment.mjs` | O material acompanha a peça ao girar. A ferramenta identifica a estação e E abre a interação pela rota de aproximação. A amostragem atual é 1,6 cm nas estações e 4,1 cm no terreno. |
| Forja e emissivos | `Art/Gaussian/painted-forge.mjs`, `forge-fire.mjs` e `Assets/Lumon/HubWorld/SplatForgeFire.cs` | Compare duas fases do fogo. A abertura continua escura atrás da chama. O calor alcança a soleira e o piso próximo. Confira a inspeção com F. |
| Guardiã e personagens | [Guia da guardiã jogável](../../../../Docs/Guardiao2D.md), `Art/Gaussian/guardian_walk.py`, `painted-guardian.py` e `painted-actors.py` | A guardiã usa um PNG, oito direções e pés alinhados ao chão. Observe saída, troca de apoio, parada e movimento reduzido. Compare o rosto entre quadros e a escala junto à ferreira. |
| HUD e painéis | `Assets/Lumon/HubWorld/UI/Hub.uxml` e `Hub.uss` | Use os tokens de papel, painel, bronze e jade existentes. Títulos em Source Serif 4 e corpo em Atkinson Hyperlegible. Verifique o texto localizado e a posição do marcador perto do rosto e da ferramenta. |

As cores do mundo vêm das rampas de cada material. A pedra recebe sombra fria, o latão ganha luz quente na aresta e o cobre patinado ocupa as coberturas. A luz âmbar da chama tem alcance local. Para uma construção com outra função, escolha uma ferramenta e um foco de luz próprios. O panorama pintado, os volumes arquitetônicos giráveis e o relevo da guardiã têm funções diferentes na composição.

## Reproduzir e revisar

Abra o clone na versão do Unity indicada em `ProjectSettings/ProjectVersion.txt`. A cena já contém os assets jogáveis. Para refazer a praça depois de editar a autoria, execute `node Art/Gaussian/build_lumon_splats.mjs hub-world` na raiz. Esse comando atualiza volumes, pigmentos e sombras. Os módulos Node utilizados na exportação acompanham o projeto.

Para a guardiã, `python Art/Gaussian/painted-guardian.py` lê o PNG editável e reexporta o relevo. O guia da personagem descreve como recompor a folha a partir das peças pintadas. Ao mudar uma estação ou seu posicionamento no manifesto, confira a instalação de cena com `HubSplatBuilder.InstallAndBuild`. Para recompilar a cena existente, use o comando do [README](../../../../README.md).

Com a build Windows pronta, execute na raiz:

```powershell
python Plugins/lumon-design/skills/lumon-design/scripts/capturar.py
```

O percurso abre as oito interações, fotografa a praça em 1920 × 1080 e registra a câmera e a posição. O script imprime a pasta das capturas. Para comparar duas revisões, execute-o em cada build e abra o mesmo nome de tomada. Se a mudança atingir circulação, presença dos jogadores ou cores de equipe, exerça também a sala com `python QA/25D-Hub-Six/test_room.py <pasta-de-saida>`.
