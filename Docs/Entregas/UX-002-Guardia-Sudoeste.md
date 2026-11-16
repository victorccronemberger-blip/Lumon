# UX-002 · Caminhada sudoeste da Guardiã

**Estado:** prova técnica concluída no Unity. Aceite visual pendente de correção da passada.

## Tarefa e responsáveis

- **Codex** — manter a leitura de aprendiz de 16 a 18 anos, a identidade do traje e a câmera fixa. Inspecionar a prova em escala de jogo e decidir o aceite.
- **Higgsfield** — fonte de movimento, job `b7159dd3-775b-4392-b7ed-609a4eaeeda9` com `gpt_image_2_6`.
- **Claude Code · Opus 5.5** — preparar a sequência, âncoras e transição da passada para repouso. Conferir a ida e volta do ciclo e registrar o pé de apoio por quadro.
- **Claude Code · Fable 5.2** — verificar a integração no caminho de movimento existente e os casos de parada e limite da praça. Registrar os testes e reproduzir a captura.

## Fontes e observações

- [Guia de arte da Guardiã 2D](../Guardiao2D.md).
- [Fonte animada](../../Art/Guardian2D/source/guardian-apprentice-walk-southwest.gif), 480 × 480, oito quadros distintos, 110 ms por quadro.
- [Quadros da fonte](../../Art/Guardian2D/review/walk-source-southwest.png). A sequência mostra fases distintas da passada. A altura da sola varia entre quadros na fonte, por isso o recorte precisa preservar a âncora de cada apoio.
- [Oito rumos em repouso capturados no Unity](../../Art/Guardian2D/review/idle-directions/unity-oito-rumos.png) são a referência de integração. O ciclo sudoeste ainda aguarda captura equivalente no motor.
- SHA-256 da fonte animada em 15/11/2026  
  `a2833e8a572c9ab975f7b06d629620bce6b26a02d52b6c7795e1baf48b5d82b4`

## Execução de 15/11/2026

- Opus 5.5 e Fable 5.2 receberam a ficha com o mesmo contrato. O Opus prepara a fonte em `Art/Guardian2D/walk/southwest`. O Fable integra e verifica a cena em `Assets/Lumon/GuardianReview`.
- O teste preliminar na fonte localizou a cabeça com variação máxima de 3 px em X e 2 px em Y. A última linha opaca das solas variou entre 440 e 456 no GIF. A revisão final depende de acompanhar o pé de apoio em relação ao chão no Unity.
- Saídas das duas execuções do Claude Code em `Logs/ux002-opus-southwest.json` e `Logs/ux002-fable-southwest.json`. A conclusão de cada execução deve ser conferida nesses resultados ou no processo ativo correspondente.

- [Prancha do Opus em escala de 720p](../../Art/Guardian2D/walk/southwest/preview-720p-strip.png). Seis poses foram selecionadas entre os oito quadros de origem. O manifesto estima 5,23 px de deriva horizontal no apoio e 21,28 px de avanço vertical por passo. São estimativas da fonte. A captura do motor deve mostrar a trajetória real do apoio.

## Checklist de aceite

- [x] Receber, identificar e inspecionar a fonte.
- [x] Registrar a ordem dos quadros, a sola de apoio e a âncora que conecta o sprite ao chão. [Manifesto da prova](../../Art/Guardian2D/walk/southwest/clip.json).
- [x] Exercitar o loop de diagnóstico pelo deslocamento efetivo. Confirmar início da passada, parada e tentativa de andar contra o limite.
- [x] Capturar e revisar uma passada completa na praça em 720p e 1080p. Gravar a análise do contato do pé.
- [ ] Corrigir a trajetória do apoio e confirmar a troca natural de pernas nas transições. Retestar antes de propagar para os outros sete rumos.

## Resultado na câmera do Unity

- Na revisão final da prova, o Unity executou 14 verificações, 550 passos do controlador e dez momentos capturados em cada resolução. O Fable implementou a integração e o roteiro de teste. O teste usa o método de movimento da cena para percorrer os seis quadros em ordem, reiniciar o ciclo e voltar ao repouso ao soltar ou alcançar o limite. A velocidade da prova foi de 1,56 m/s para 1,37 m por ciclo.
- [Movimento capturado em 720p](../../Art/Guardian2D/review/walk-southwest/unity-sudoeste-720.gif). A prévia usa os recortes da mesma câmera e reinicia o deslocamento ao repetir.
- [Prova em 720p](../../Art/Guardian2D/review/walk-southwest/review-720.png) e [em 1080p](../../Art/Guardian2D/review/walk-southwest/review-1080.png), com recortes em escala real.
- Entre o primeiro e o quarto quadro, a projeção do ponto anotado para a mesma sola recua 7,63 px na horizontal e desce 20,95 px na vertical em 720p. Em 1080p, desce 31,44 px. A altura e a estabilidade da câmera passaram. A trajetória do pé e os saltos entre fases mantêm o aceite aberto.
- [Evidência do Unity](../../Art/Guardian2D/review/walk-southwest/evidence.json), [projeção do apoio](../../Art/Guardian2D/review/walk-southwest/support-review.json) e [revisão do Opus](../../Art/Guardian2D/walk/southwest/animation-review.md).
- O Higgsfield gerou uma segunda fonte orientada pela trajetória diagonal do apoio. O job `b38f4307-b2d8-4d5c-83cd-9da6e40c334b` produziu [oito quadros](../../Art/Guardian2D/source/guardian-apprentice-walk-southwest-v02.gif). A [prancha](../../Art/Guardian2D/review/walk-source-southwest-v02.png) ainda concentra poses na mesma perna visível. A fonte permanece em diagnóstico.

## Revisão da correção do apoio

- O ensaio lógico da entrada de diagnóstico separada passou no Unity 6000.3.27f1. A entrada preserva a cena ativa. O ensaio usa imagens sintéticas e confirma apenas o funcionamento do roteiro.
- A análise agora projeta o ponto de apoio anotado em cada passo do controlador, inclusive entre trocas de quadro. Na prova original, o pé esquerdo chegou a 34,24 px de distância do ponto inicial de apoio, e a sola se deslocou até 14,42 px enquanto um único quadro estava na tela em 720p. Esse movimento entre poses precisa entrar no reteste.
- A correção da arte usa a pintura existente e registra o ponto de contato no PNG, separado do pivô do tronco. A revisão precisa medir a trajetória na câmera e inspecionar a transição da perna em balanço.
- Para capturar a correção em separado, execute `Lumon.GuardianReview.GuardianWalkReview.ReviewSouthWestGrounding` no Unity em batch mode com gráficos. Depois rode `python Art/Guardian2D/review_walk_southwest.py --grounding`.

### Resultado da correção localizada

O Opus preservou a posição do tronco e colocou o ponto de contato em metadados separados do pivô. A entrada de diagnóstico do Fable carregou essa versão sem substituir a cena. O Unity registrou 14 verificações, 550 passos do controlador e dez momentos em cada resolução. Os hashes do manifesto e dos PNGs coincidem com a evidência. O revisor rejeita uma captura se qualquer um desses arquivos mudar.

| Medida em 720p | Fonte anterior | Correção localizada |
| --- | --- | --- |
| Maior movimento do ponto de apoio entre trocas de pose | 13,22 px | 1,44 px |
| Maior distância do ponto inicial durante um apoio | 34,24 px | 14,78 px |
| Maior deslocamento enquanto uma só pose é exibida | 14,42 px | 14,42 px |

- [Prancha 720p](../../Art/Guardian2D/review/walk-southwest-grounding/review-720.png), [prancha 1080p](../../Art/Guardian2D/review/walk-southwest-grounding/review-1080.png) e [movimento em escala real](../../Art/Guardian2D/review/walk-southwest-grounding/unity-sudoeste-720.gif).
- [Rastro do apoio a cada passo](../../Art/Guardian2D/review/walk-southwest-grounding/support-review.json) e [captura técnica](../../Art/Guardian2D/review/walk-southwest-grounding/evidence.json). Os [recortes de inspeção do Opus](../../Art/Guardian2D/walk/southwest-grounding/animation-review.md) localizam os defeitos da perna deformada.
- O aceite visual permanece aberto. As seis poses corrigem as posições nos pontos de troca, mas o pé ainda acompanha o tronco durante os intervalos. A revisão em 1080p também exige conservar o contorno da bota nas poses deformadas. A opção para o próximo teste é animar a perna plantada em relação ao tronco ao longo da fase, com a outra perna em balanço.

Para preparar de novo a correção da fonte, rode `python Art/Guardian2D/ground_walk_southwest.py` antes do método de diagnóstico.

### Prova de 18 poses

O Opus manteve o ciclo de 0,88 s e preparou três posições distintas da perna de apoio para cada uma das seis poses. O Fable reaproveitou o diagnóstico do Unity e isolou os PNGs, os GUIDs e a evidência. O replay de 15/11/2026 confirmou 14 verificações, 550 passos do controlador e 22 momentos em cada resolução. O manifesto e os 18 quadros foram reproduzidos com os mesmos hashes. A cena ativa preservou o SHA-256 dos oito repousos.

| Medida em 720p | Seis poses corrigidas | Dezoito poses |
| --- | --- | --- |
| Maior movimento do ponto de apoio entre trocas de pose | 1,44 px | 1,75 px |
| Maior distância do ponto inicial durante um apoio | 14,78 px | 4,11 px |
| Maior deslocamento enquanto uma só pose é exibida | 14,42 px | 3,60 px |

A prova reduz o deslizamento durante a passada. O aceite visual segue aberto devido aos saltos da perna em balanço e do tronco nas trocas das poses-base, à canela que perde união com a perna de trás e à compressão do joelho. A animação seguinte precisa de camadas pintadas para tronco, braços e cada perna, incluindo as partes que ficam encobertas. Isso permite desenhar os movimentos de balanço e a transferência de peso ao longo de todo o ciclo com a trajetória de apoio medida nesta prova.

- [Captura em movimento em 720p](../../Art/Guardian2D/review/walk-southwest-subframes/unity-sudoeste-720.gif), [prancha em 1080p](../../Art/Guardian2D/review/walk-southwest-subframes/review-1080.png), [registro de apoio](../../Art/Guardian2D/review/walk-southwest-subframes/support-review.json) e [evidência técnica](../../Art/Guardian2D/review/walk-southwest-subframes/evidence.json).
- [Revisão visual do Opus](../../Art/Guardian2D/walk/southwest-subframes/animation-review.md). Reprodução com `python Art/Guardian2D/ground_walk_southwest_subframes.py`, método Unity `Lumon.GuardianReview.GuardianWalkReview.ReviewSouthWestSubframes` e `python Art/Guardian2D/review_walk_southwest.py --subframes`.
- Logs dos papéis em `Logs/ux002-opus-subframes.jsonl` e `Logs/ux002-fable-subframes.jsonl`. Resultado do Unity em `Logs/ReviewSouthWestSubframes.log`.

## Reprodução da base

No Unity 6000.3.27f1, abra **Lumon → Open 2D Guardian Review**. A cena contém os oito rumos em repouso e usa a função de movimento que recebe WASD ou setas. A prova da passada deve partir dessa cena, capturar a mesma câmera e registrar a configuração aplicada.

O método Unity `Lumon.GuardianReview.GuardianWalkReview.InstallAndVerifySouthWest` registra a prova. O último replay passou com `sceneSaved=false`. O SHA-256 da cena ativa permaneceu idêntico ao dos oito repousos. A troca da arte ativa exige que o manifesto registre o aceite visual. Rode `python Art/Guardian2D/review_walk_southwest.py` para conferir os hashes dos PNGs, recortar as vistas e projetar o apoio anotado sobre as posições capturadas.
