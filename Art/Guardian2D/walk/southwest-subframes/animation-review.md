# Caminhada sudoeste · prova de 18 subposes

**Estado:** `proof-not-accepted`. Prova separada. A v1 (`walk/southwest`) e o grounding (`walk/southwest-grounding`) não foram alterados. O script confere os hashes das duas pastas antes e depois de rodar.

## Reprodução

```
python -B Art/Guardian2D/ground_walk_southwest_subframes.py
```

- O script grava somente em `walk/southwest-subframes/`.
- Do script de grounding, importa apenas funções e constantes, sem bytecode. Nunca chama o `main()` nem o `previews()` dele.
- Duas execuções seguidas geraram os 25 arquivos com hashes idênticos.
- Os 18 PNGs têm hashes distintos entre si.

## O que foi feito

- **Base.** As 6 pinturas v1, a máscara de apoio do grounding (`POLY` com o alpha da pintura) e os mesmos deslocamentos de bota `d_k`.
- **Subposes.** São três por fase, `j = 0, 1, 2`. Os deslocamentos ficam centrados no quadro base:
  `D(y) = ramp(y)·d_k + knee(y)·(j−1)·Δ`.
  - `ramp` é a rampa do quadril até o cano do grounding.
  - `knee` é uma rampa que começa no joelho, a 45% entre o quadril e o cano, com inclinação máxima no joelho e zero no cano.
  - Abaixo do cano, a bota só se translada, sem deformar.
- **Avanço por subpose.** `Δ = (+13, −6)` px de fonte, que dão (+4,85; −2,24) px em 720p. O valor exato seria (+12,67; −6,33).
  - Arredondei para inteiro porque, com o valor fracionário, a bota das subposes 0 e 2 saía mais borrada que a da 1. A variância do Laplaciano caía de 743 para 549 no quadro 00. Com o arredondamento, a nitidez fica igual nas três subposes.
  - Resíduo do calcanhar medido por par plantado em 720p: +0,12 px dentro de uma fase e −0,25 px na passagem entre fases.
- **O que não muda.** Tronco, cabeça, braços, quadril e perna de balanço ficam com os pixels da v1.
  - Dentro de uma fase, 0 px mudam acima do quadril (conferido).
  - O sprite inteiro nunca se desloca. O pivô é fixo e igual ao do grounding: (0.517494; 0.107414).
- **Regressão.** A subpose 1 deveria reproduzir o grounding.
  - O quadro 03 sai idêntico.
  - Os outros quadros diferem em 8 a 180 px, com até 99 níveis de cor. Ficam nas linhas 236–237 da v1 (topo do polígono) e na costura do joelho.
  - O PNG do grounding em disco foi gerado por uma versão do script com campos de costura (`seamBandRemovedPx`) que não estão no `ground_walk_southwest.py` atual.

## Conferências (no script, sobre os PNGs relidos do disco)

- Os 18 arquivos estão em ordem, de `frame-00` a `frame-17`, todos com 296 × 526 e nenhum pixel opaco na borda.
- O `contactX/contactY` do `clip.json` cai sobre um pixel com alpha ≥ 128 em todos os quadros. O ponto é o canto do calcanhar da bota de apoio.
- O loop tem 16 pares plantados e duas trocas de apoio, em 08→09 e 17→00.
- **Distância do calcanhar até o ponto de pouso** em 720p, com a raiz andando (−4,73; +2,36) por subpose:
  - nesta prova, no máximo 0,35 px;
  - no grounding, com cada quadro repetido três vezes, 5,29 e 10,58 px.

  Isso aparece em `preview-720p-planted-check.png`: o calcanhar fica sob a cruz nas linhas "sub" e escorrega nas linhas "gr".
- Bota sem deformação: a contagem opaca da bota deslocada é igual entre as três subposes, e no máximo 53 px maior em 08.
- `cadenceMs` = 48,89 (880 / 18).
- No GIF, os quadros duram 16 × 50 ms + 2 × 40 ms, para fechar 880 ms. O formato GIF só aceita múltiplos de 10 ms.

## Defeitos vistos em escala real e a 3× (coordenadas no canvas de 296 × 526)

1. **Lascas e corte da canela de trás**, em `preview-knee-3x.png` e na prancha de 1080p.
   - Quadros 06–08 e 15–17 (bases 2 e 5). Na borda direita da coxa e do joelho de apoio, x ≈ 172–186 e y ≈ 300–345, sobra uma lasca de 1–2 px da faixa de costura da v1.
   - Logo abaixo, em x ≈ 175–185 e y ≈ 340–390, o topo da canela de trás aparece cortado reto. A área que a coxa cobria ficou transparente.
   - O defeito já existia no grounding (quadro 05) e **cresce** nas subposes 0 e 2, porque a perna se afasta mais da costura.
2. **Corte entre as pernas.**
   - Quadro 03: fresta transparente entre a canela de apoio e a de trás, em x ≈ 170 e y ≈ 340–405.
   - Quadro 12: listra vertical clara de 1–2 px em x ≈ 160 e y ≈ 330–400.
3. **Compressão da perna.** Nos quadros 08 e 17, a canela é comprimida 28–29 px de fonte. O joelho fica baixo e a leitura é de agachamento. No quadro 09, o estiramento de +29 px ainda lê como passada longa em 720p.
4. **Alternância frágil.**
   - A perna de balanço, os braços e o tronco ficam parados por três subposes, 147 ms, e então saltam. Mudam de 38 a 42 mil px acima do quadril a cada troca de base.
   - A perna de apoio desliza de forma contínua enquanto a de balanço anda "em degraus". A 20 fps, isso se percebe como engasgo.
5. **Trocas de apoio.** Os saltos de 08→09 e 17→00 continuam como no grounding: o pé que sai desce cerca de 10 px de tela.
6. **Pivô.** Herdado do grounding: o calcanhar de contato fica até cerca de 11 px de tela abaixo da raiz.

## Arquivos

- `frame-00..17.png` e `clip.json`. O `clip.json` traz em `measurements` o mapa base/subpose, os deslocamentos, os pares, a deriva e os hashes.
- `preview-720p-strip.png` e `preview-1080p-strip.png`: em cima, o grounding com cada quadro repetido três vezes; embaixo, as 18 subposes; o pivô é fixo.
- `preview-720p-compare.gif`: grounding à esquerda e subposes à direita, com o mesmo passo de raiz. O círculo vermelho marca o ponto de pouso e o ponto ciano, o contato atual.
- `preview-720p-planted-check.png`, `preview-legs-1.5x.png` e `preview-knee-3x.png`.
