# Caminhada sudoeste · revisão da animação

**Estado:** prova preparada, **não aceita**. Os defeitos da fonte estão listados abaixo. Alternância, apoio e deslize são hipóteses até a captura no Unity.

## Reprodução

```
python Art/Guardian2D/prepare_walk_southwest.py
```

O script confere o SHA-256 da fonte (`a2833e8a…82b4`). Ele lê a fonte em `source/` e o repouso aprovado: `idle-directions.json` e as funções puras de `prepare_idle_directions.py`, sem executá-lo e sem criar cache. Grava apenas nesta pasta: `frame-00..05.png`, `clip.json`, `preview-720p-strip.png` e `preview-720p-travel.gif`. Também falha se o repouso SouthWest aprovado deixar de reproduzir o próprio registro. Nada desse repouso é regravado.

## Quadros

Todos os quadros estão na resolução nativa do GIF, em uma tela comum de 232 × 462 px. O recorte só translada a arte, sem redimensionar, espelhar ou interpolar. `pivotX = 0.52232` é igual em todos: é a âncora do tronco (centro do broche mais o deslocamento broche→apoio do repouso aprovado, convertido para a escala da caminhada). `pivotY` é a última linha da sola de apoio em cada quadro.

| saída | fonte | fase | apoio | sola (px fonte) | altura a 720p |
|---|---|---|---|---|---|
| frame-00 | 0 | contato: calcanhar esquerdo toca, direito sai (7 px acima) | left | 444 | 161 |
| frame-01 | 1 | carga: calcanhar direito sobe | left | 450 | 163 |
| frame-02 | 3 | direito balança atrás, alto | left | 454 | 165 |
| frame-03 | 5 | contato: calcanhar direito a 3 px do chão, esquerdo em ponta | left | 440 | 159 |
| frame-04 | 6 | carga: calcanhar esquerdo sobe | right | 452 | 164 |
| frame-05 | 7 | esquerdo balança atrás, alto | right | 455 | 165 |

Convenção: `left` é a perna próxima da câmera no rumo sudoeste. É a bota com a fivela visível e a perna que encobre a outra. `supportFoot` marca o pé dono da sola mais baixa, que define `pivotY`. O script falha se essa anotação não coincidir com a medida.

Por essa regra, os rótulos ficam **4 left / 2 right**. Os contatos (frame-00 e frame-03, também em `measurements.contactFrames`) estão em apoio duplo. Em frame-03, o pé direito que chega fica só 3 px acima da ponta esquerda, margem parecida com o desvio de 2,5 px do broche em y entre as fontes 0–3 e 4–7. Pela trajetória, a alternância é 3 + 3: o pé direito carrega de frame-03 a frame-05, e o esquerdo de frame-00 a frame-02. Leia a alternância pelos contatos, não pela contagem de rótulos.

## Ordem e descarte (após ver os oito quadros)

O GIF dá **5 quadros ao passo esquerdo (0–4) e 3 ao direito (5–7)**. O pé plantado recua cerca de 16 px/quadro em 1→4 e 47 px em 6→7. O `GuardianSpriteFacing` dá a mesma distância a cada quadro, então nenhuma `distancePerCycle` zera os dois passos. Deriva máxima do pé plantado a 720p, por ordem candidata:

| ordem | deriva máx. | deslize máx. por quadro |
|---|---|---|
| 0–7 (fonte) | 13,8 px | 6,9 px |
| **0,1,3,5,6,7 (escolhida)** | **5,2 px** | **6,0 px** |
| 0,1,4,5,6,7 | 4,1 px | 4,1 px |
| 0,2,4,5,6,7 | 4,1 px | 4,1 px |

Descartei os quadros 2 e 4. A ordem escolhida forma pares equivalentes nos dois passos: (0,5) contato, (1,6) carga com pé de trás a 427/426 px e (3,7) balanço atrás com pé a 407/406 px. O quadro 4, joelho alto, não tem par no passo direito. Usado em uma só passada, ele mancaria a leitura. A ordem 0,1,4 desliza 1,1 px menos e é a alternativa se o Codex preferir menos deslize a passos simétricos. Nenhum quadro foi criado.

## Calibração

- Passada medida por casamento de molde da bota de apoio: esquerdo 116 px + direito 112 px = 228 px de fonte. Na conferência pela separação das botas nos contatos, 94 + 128 = 222 px.
- `referenceHeightPx = 442`: maior altura da cabeça à sola de apoio em apoio simples, com perna estendida. É medida nas fontes 2 e 4, que foram descartadas; entre os quadros mantidos, o máximo é 441, diferença irrelevante. A escala é 165/442 = 0,3733 px de tela por px de fonte. Essa medida vale para o ciclo todo.
- `suggestedDistanceMeters = 1.3709`. A câmera ortográfica tem 87,8 px/m. A diagonal sudoeste projeta (−62,1; +31,0) px por metro, com o sprite em billboard e inclinação de 30°.
- `cadenceMs = 146.67`: o ciclo da fonte (8 × 110 ms) dividido em 6 poses. A fonte implica 1,56 m/s. Com `Speed = 2.8` m/s, o motor troca de quadro a cada 81,6 ms, cerca de 4,1 passos/s, o que dá leitura apressada para uma caminhada.

## Defeitos e limitações observados

1. **Deslize vertical (principal):** o pé plantado recua só na horizontal na fonte (−5 px em y nos trechos planos, contra −47,5 px esperados para a diagonal sudoeste). Como o `pivotY` fica na sola, o pé desce com o personagem: **≈ 21 px por passo em 720p** (≈ 32 px em 1080p). Nenhuma distância corrige isso. Corrigir exige redesenhar o recuo do pé de apoio para cima e para a direita.
2. Salto de perna em um quadro: a perna de balanço passa de trás para o contato à frente em 02→03 e 05→00. Isso vem de descartar o quadro 4, e a fonte já mostrava o mesmo salto em 7→0.
3. Identidade das pernas: a bota de apoio sempre aparece com a fivela e à frente. Da fonte 5 para a 6, a fivela passa da bota de trás para a da frente. A alternância só se sustenta pela trajetória das botas, não pela aparência.
4. Os braços quase não balançam e não se alternam com as pernas.
5. Proporção: cabeça→broche é 5,9% maior que no repouso aprovado. Uma escala para igualar a cabeça usaria `referenceHeightPx ≈ 468`, mas a figura ficaria com cerca de 156 px. Mantive 165 px.
6. Oscilação vertical: a altura a 720p varia de 159 a 165 px, com os contatos mais baixos. O sentido é natural, mas a amplitude de cerca de 6 px passa um pouco do esperado.
7. Saída do repouso: o motor zera a fase e começa em frame-00 (contato), e a cabeça desce cerca de 4 px em relação ao repouso. O tronco não salta em x, porque o deslocamento horizontal vem do repouso aprovado. Ao parar, o motor volta direto ao repouso, sem transição desenhada.
8. Alpha do GIF é binário. O script aplica uma suavização de 1 px para dentro e espalha a cor sob o alpha 0. Remove de 0 a 4 px soltos por quadro.

## Evidência da prévia local (escala constante)

- `preview-720p-strip.png`: repouso aprovado (165/980) e os 6 quadros (165/442) nos pivôs, com linha de chão e marca de 165 px. As alturas medidas na imagem foram idle 166 e caminhada 159–165 px.
- `preview-720p-travel.gif`: simula o motor a 720p. A raiz anda para sudoeste a 2,8 m/s, o quadro avança por distância e o ciclo dura 1,3709 m. O círculo vermelho marca onde o pé pousou no último contato (frame-00 ou frame-03), e o ponto ciano segue a sola desse pé. O afastamento entre os dois em cada passo de 3 quadros mostra o defeito 1.
- Duas execuções seguidas produziram hashes idênticos em todos os arquivos.
- Os campos são compatíveis com `GuardianWalkReview.Validate`, conferido apenas por leitura. Os valores dão 1,56 m/s, dentro do intervalo de 0,4 a 2,8 m/s, e cadência de 146,67 ms, dentro de 40 a 250 ms. Ainda falta a captura no Unity (Fable/Codex).
