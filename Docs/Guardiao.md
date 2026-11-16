# Guardião do Observatório

Um adulto compacto, com rosto e sobrancelhas legíveis na câmera da praça. Casaco curto de lona azul-petróleo, forro ameixa e um broche de latão com cristal da aurora junto à gola. O broche concentra o contraste do traje perto do rosto. Mechas esculpidas, dobras pintadas e um bolso de mapas caracterizam alguém que viaja entre as ilhas do Observatório.

| Material | Cor de referência |
| --- | --- |
| Lona do casaco | #245A62 |
| Forro de tecido | #653C55 |
| Linho da gola | #D5C1A0 |
| Latão pintado | #A47C43 |
| Cristal da aurora | #53D7C9 |

## Folha de referência

[Frente, perfil e costas](../Art/Guardian/guardiao-observatorio-turnaround.png)

A folha estabelece as três massas do traje: gola clara, casaco azul-petróleo e calça marrom. Use a frente para conferir a largura dos ombros e a posição do broche. Use o perfil para esculpir nariz, queixo e coque. A capa ameixa deve formar uma silhueta contínua entre perfil e costas. A posição do bolso e a construção das botas precisam ser reconciliadas na malha antes da texturização.

## Checklist

- [x] Criar um projeto Unity independente.
- [x] Revisar a folha de referência em frente, perfil e costas.
- [x] Verificar que a câmera mantém a escala de referência em nove posições da área de movimento.
- [x] Modelar os volumes principais em escala métrica e importar a prova temporária no Unity.
- [x] Capturar a prova de volume com a câmera fixa nos oito ângulos em 720p.
- [ ] Refinar rosto, cabelo, costuras e peças do traje. Preparar topologia, UVs, textura pintada e esqueleto.
- [ ] Importar o modelo texturizado e animado no pivô do Guardião.
- [ ] Revisar repouso e caminhada em oito direções, incluindo diagonais, à escala normal do jogo.
- [ ] Inspecionar rosto, gola, broche e contato dos pés em 1280×720 e 1920×1080.
- [ ] Capturar o mesmo modelo na câmera fixa da praça e no enquadramento do ateliê.

A cena técnica estabelece uma altura inicial de 120 pixels em 720p para um Guardião de 1,8 m. O Unity mediu 136,9 pixels nas nove posições verificadas em 720p. A medição usa o eixo dos pés ao topo da cabeça. O aceite visual depende da leitura dos detalhes no modelo em movimento e no cenário final.

A aparência usa materiais independentes de idioma. PT-BR, EN e ES são obrigatórios na interface. Os textos de menu ficam no catálogo de localização.

## Prova de volume

- [Master Blender](../Art/Guardian/CH_Guardian_master.blend) e [medidas da folha](../Art/Guardian/asset-brief.md).
- [Capturas em oito ângulos](../Art/Guardian/review/02-blockout/unity-oito-angulos.png), com recortes em escala de tela. Altura do FBX conferida em 1,800 m, com 142,9 a 148,2 px para a geometria real. A régua vertical tem 136,9 px. A diferença vem da profundidade dos pés e do cabelo na projeção inclinada.
- A sobreposição registrada com a folha foi de 0,912 na [frente](../Art/Guardian/review/02-blockout/diff_front.png), 0,944 no [perfil](../Art/Guardian/review/02-blockout/diff_left.png) e 0,873 nas [costas](../Art/Guardian/review/02-blockout/diff_back.png). O limite da fase de volume é 0,85. O limite da fase de formas é 0,90. A maior correção pendente está nas costas: bolso assimétrico, coluna do torso e profundidade do coque.
- O comando Unity Editor `Lumon.GuardianReview.GuardianBlockoutCapture.Capture` importa `CH_Guardian_review.fbx` temporariamente para capturar a cena em 720p. O FBX de revisão é gerado com `Art/Guardian/build/02_export_review.py`. A cena salva continua aguardando o modelo de produção.
