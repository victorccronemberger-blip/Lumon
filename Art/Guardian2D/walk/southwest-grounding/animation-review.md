# Caminhada sudoeste · prova de apoio plantado (grounding)

**Estado:** `proof-not-accepted`. É uma prova separada da v1 e não substitui nada. A trajetória do apoio foi corrigida nos quatro pares plantados. As trocas de apoio ainda saltam, e há defeitos de costura e de proporção listados abaixo.

## Reprodução

```
python -B Art/Guardian2D/ground_walk_southwest.py
```

O script lê apenas `walk/southwest/` (os seis quadros v1 e `clip.json`) e grava apenas nesta pasta. Ele falha nestes casos:

- algum arquivo da v1 muda durante a execução;
- alguma linha acima de 238 (cabeça, tronco, braços, cinto, raízes do quadril) difere da v1 em qualquer byte;
- algum pixel pintado fora do polígono de apoio muda sem estar coberto pela perna deslocada ou pelo reparo de costura;
- algum pixel opaco toca a borda do canvas;
- a lista de campos do clip deixa de ser a da v1.

Os pixels de outras partes que a perna deslocada passou a cobrir são contados por quadro em `measurements.grounding.pixels`. São 59, 4, 58, 12, 176 e 25. Quase todos são a perna de trás, que a perna de apoio já encobria. Duas execuções seguidas deram hashes idênticos em todos os arquivos. O script não importa nem executa `prepare_walk_southwest.py`, porque o `main()` dele apaga os quadros v1.

## Contrato aplicado

- Escala 165/442 = 0,3733 px de tela por px de fonte.
- Por slot, a raiz anda (−14,19; +7,09) px de tela a 720p (1,3709 m / 6). A sola plantada precisa então recuar (+14,19; −7,09) px de tela em relação ao pivô, o que dá **(+38,0; −19,0) px no canvas**. Em `contactX/contactY` normalizados, com origem inferior esquerda, os dois valores crescem.
- **Pivô fixo** e igual nos seis quadros: `pivotX = 0.517494`, `pivotY = 0.107414` no canvas de 296 × 526. O canvas é o da v1, de 232 × 462, com 32 px de margem igual em todos os lados.
  - `pivotX` é a mesma âncora de tronco da v1, deslocada pela margem.
  - `pivotY` é a linha do chão sob a raiz no meio do apoio (k = 1). No contato (k = 0), o calcanhar fica 19 px de canvas (7,1 px de tela) abaixo do pivô. No fim do apoio (k = 2), fica 19 px acima.
- Torso, rosto, braços, cinto e raízes do quadril ficam com os pixels da v1. O script confere byte a byte tudo acima da linha 238 da v1. Abaixo dela, as mãos só mudam se a perna deslocada encostar nelas, e isso entra na contagem acima. Como o pivô não varia mais por quadro, o sobe-e-desce do tronco vem só da pintura v1. A oscilação extra que a v1 produzia com `pivotY` variável deixou de existir.

## Como os contatos foram marcados

- **Ponto material:** é o canto do calcanhar da bota de apoio, o pixel opaco (alpha ≥ 128) que maximiza `y + 0,5·x` na região da bota. Esse é o ponto que fica fixo do toque do calcanhar até o pé plano.
- **Por que não o ponto médio da sola:** testei primeiro o meio do segmento entre calcanhar e ponta (`CONTACT_FRACTION = 0.5`). No toque do calcanhar, esse ponto fica cerca de 20 px acima do chão. A correção exigida subiu para ±33 px de deformação e ficou descartada.
- **Ponta:** é o pixel mais à frente entre as linhas baixas da bota. Ela é usada só para medir o pé que sai na troca de apoio.
- **Medição:** `contactX/contactY` são medidos de novo no alpha dos PNGs finais, dentro da região da bota deslocada. Essa medida usa a máscara que o próprio script deslocou, então não é totalmente independente.
- **Conferências independentes:**
  - `preview-720p-planted-check.png` desenha cada quadro na posição percorrida pela raiz, em 720p, e recorta ao redor do ponto de pouso daquele apoio, com a cruz ciano. Inspecionei a imagem: o canto do calcanhar fica sob a cruz nos três quadros de cada apoio.
  - O casamento rígido de molde da bota entre quadros (`bootTemplateDxDyRms`) **não serve** de confirmação. O RMS vai de 64 a 79 porque a bota gira do calcanhar para o pé plano. Ele fica registrado só para transparência.

## Pares realmente plantados

O apoio por trajetória é 3 + 3. O frame-03 passou a ser `right` (na v1 era `left`, pela sola mais baixa) porque o pé direito é o que pousa ali e carrega 03–05.

| par | pé | tipo | passo do calcanhar (canvas) | resíduo a 720p |
|---|---|---|---|---|
| 00→01 | left | **plantado** | (+38,0; −19,0) | (0,00; 0,00) |
| 01→02 | left | **plantado** | (+38,0; −19,0) | (0,00; 0,00) |
| 02→03 | left sai | troca, não plantado | ponta (+60,1; +10,0), estimativa | **(+8,2; +10,8)** |
| 03→04 | right | **plantado** | (+38,0; −19,0) | (0,00; 0,00) |
| 04→05 | right | **plantado** | (+38,0; −19,0) | (0,00; 0,00) |
| 05→00 | right sai | troca, não plantado | ponta (+51,2; +7,0), estimativa | **(+4,9; +9,7)** |

Os números das trocas são estimativas, porque comparam a ponta de uma bota plana com a ponta de uma bota em impulso. A sequência do `preview-720p-travel.gif` confirma o salto: no quadro 03, a bota esquerda que sai aparece cerca de 10 px de tela abaixo do ponto onde estava plantada.

Deslocamento rígido aplicado à bota de apoio, em px de canvas, de 00 a 05:

| quadro | 00 | 01 | 02 | 03 | 04 | 05 |
|---|---|---|---|---|---|---|
| deslocamento | (−2,5; +16) | (−0,5; −8) | (+2,5; −22) | (−0,5; +23) | (+5,5; −10) | (−5,5; −23) |

A altura do chão é comum aos dois apoios. O x do pouso de cada apoio foi escolhido para minimizar o maior deslocamento.

## Método

1. Desenhei um polígono por quadro (`POLY` no script) sobre a perna de apoio, que é sempre a camada da frente, e o intersectei com o alpha da pintura e com as linhas abaixo de 238. O contorno da máscara aparece em magenta em `preview-legs-1.5x.png`.
2. Apaguei a perna de apoio da v1. Nas costuras em que o polígono atravessa a pintura e encosta na outra perna ou no quadril, mantive uma faixa de 2 px da v1 por baixo. Sem essa faixa, aparecia uma linha clara de 1 px, vista e corrigida na segunda tentativa.
   - Onde a perna deslocada deixa essa faixa à mostra, ela viraria contorno duplicado. Por isso a faixa exposta é substituída por um prolongamento de 1–3 px da pintura vizinha da outra perna (inpaint de raio 2), em vez de ficar transparente.
   - Ficar transparente abriria uma fresta clara entre as pernas, vista no quadro 01 da terceira tentativa.
   - Pixels tratados assim: 0, 79, 57, 0, 0 e 166.
3. Apliquei uma deformação localizada só na camada da perna:
   - deslocamento zero acima da linha 238;
   - uma rampa (70% linear + 30% suave) por coxa e canela até o cano da bota (`CUFF`);
   - translação rígida da bota abaixo do cano, preservando cadarço, fivela e solado.
   
   A amostragem é inversa (`cv2.remap`) com alpha pré-multiplicado.
4. Buracos deixados pela perna: os que dão para o fundo ficam transparentes. Só um buraco totalmente fechado seria preenchido por inpaint, o que aconteceu em 2 px no total. Na primeira tentativa, o inpaint por proximidade gerou manchas marrons sob as botas nos quadros 02 e 05 e foi abandonado.
5. A perna de balanço e a bota de trás ficam com os pixels da v1.

## Defeitos concretos (por que não aceito)

1. **Saltos nas trocas de apoio.**
   - Em 02→03 e 05→00, o pé que sai desce cerca de 10 px de tela e avança 5 a 8 px.
   - A causa: nos quadros de contato (00 e 03), a bota de trás em ponta é a da v1. Pelo escopo, ela não foi tocada, e por isso fica no chão "plano" da fonte, cerca de 29 px de canvas abaixo de onde a diagonal a colocaria.
   - Para corrigir, seria preciso estender a deformação à perna de trás nos dois contatos, com cerca de (−22; −29) px em 03 e (−13; −26) px em 00, compondo-a sob a perna de apoio.
2. **Proporção da perna.**
   - O alongamento vertical que a diagonal exige é de +16 px (00) e +23 px (03) de canvas, e a compressão é de −22 px (02) e −23 px (05).
   - Na prancha a 720p, 00 e 03 leem bem como passada longa para a câmera.
   - Em 02 e 05, a canela de apoio fica curta e o joelho baixo, uma leitura levemente agachada. A pintura não foi feita para essa perspectiva, e a deformação só estica ou comprime o que existe.
3. **Costuras residuais** (vistas na ampliação 3× dos quadros finais):
   - **frame-05:** quando a coxa de apoio sobe e vai para a esquerda, aparece a borda da coxa e da canela de trás. Na v1, essa borda ficava escondida e nunca foi pintada como contorno. Na borda direita da coxa de apoio (x ≈ 149–155 do canvas v1, linhas 295–335), sobra uma serrilha escura de 2–4 px. O alto da canela de trás aparece cortado reto, porque o trecho antes coberto ficou transparente.
     - Não é a perna de apoio duplicada. É pintura da perna de trás sem acabamento.
     - Ainda assim, lê-se como emenda e precisa de retoque manual ou de uma fonte com a perna de trás completa.
   - **frame-02:** o mesmo efeito, menor, atrás do joelho, por volta de x ≈ 150 e linhas 300–320.
   - **frame-00:** pequeno entalhe na borda direita do cano da bota de apoio.
   - Na conferência 3× dos seis quadros, não aparece contorno duplicado da bota nem da perna de apoio.
4. **Pivô novo:** o pivô fixo fica no chão do meio do apoio, e não na sola mais baixa. No Unity, o calcanhar de contato aparece 7 px de tela abaixo da raiz. Sombra e ordenação que dependam da sola mais baixa precisam considerar isso.
5. **Herdados da v1, inalterados:**
   - a fivela aparece sempre na bota da frente, então a identidade das pernas não se alterna;
   - os braços quase não balançam;
   - a perna de balanço salta em 02→03 e 05→00, por causa dos quadros descartados da fonte;
   - a cabeça é 5,9% maior que no repouso.

## Arquivos

- `frame-00..05.png`: RGBA de 296 × 526, mesmo canvas e mesmo pivô.
- `clip.json`: mesmos campos e mesma ordem da v1, com `status` `proof-not-accepted`. O script falha se a lista divergir.
  - `frames[]` ganha `contactX/contactY`.
  - `source` é o mesmo da v1, o GIF de origem.
  - `measurements.contactFrames` = [0, 3].
  - `measurements.grounding` traz os hashes dos quadros v1, os deslocamentos, os pares e as contagens de pixels.
  - Em `predictions`, o deslize do apoio plantado vale 0,0 nos pares plantados, e há a estimativa das trocas.
  - O Unity (`GuardianWalkReview.Clip`) lê só `direction`, `referenceHeightPx`, `cadenceMs`, `suggestedDistanceMeters`, `frames[].file/pivotX/pivotY/supportFoot` e `status`.
- `preview-720p-strip.png`: v1 com os pivôs por quadro no alto, prova com o pivô fixo embaixo. O círculo ciano marca o contato.
- `preview-legs-1.5x.png`: v1 com a máscara em cima, resultado embaixo.
- `preview-720p-travel.gif`: raiz andando (−14,19; +7,09) px por slot em três ciclos. Vermelho marca o ponto de pouso do apoio, ciano o contato atual.
- `preview-720p-onion.png` e `preview-720p-onion-3x.png`: um ciclo sobreposto. Os contatos de 00–02 coincidem num ponto e os de 03–05 em outro, um passo à frente na diagonal.
- `preview-720p-planted-check.png`: a conferência do calcanhar sob a cruz descrita acima.

## Observação de ambiente

Durante esta execução, outro processo, fora desta tarefa, alterou três coisas:

- modificou `Art/Guardian2D/review_walk_southwest.py` às 11:34;
- modificou `Assets/Lumon/GuardianReview/Editor/GuardianWalkReview.cs` às 11:38;
- criou `Assets/Lumon/GuardianReview/Art2D/Walk/SouthWestGrounding/frame-00..05.png` às 11:35.

Não toquei nesses arquivos e só os li. **As cópias em `Assets` não batem com os quadros finais desta pasta:** os hashes diferem nos seis quadros. São de uma iteração intermediária e ainda contêm as linhas de costura e as manchas de inpaint. Antes de qualquer captura no Unity, é preciso recopiar os quadros. Fora esses, os arquivos de `Assets/` que existiam no início continuam com hash idêntico, assim como `walk/southwest/`, `source/` e `review/`.
