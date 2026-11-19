# Guardiã do Observatório · direção de arte 2D

## Cartógrafa jogável do Amanhecer

A guardiã usa a mesma linguagem pintada da ferreira e do pangolim. O rosto fica emoldurado pelo coque escuro e pelo lenço ameixa. A capa curta azul-petróleo carrega o bordado celeste, e o forro ocre separa a silhueta do casaco das calças. Na sala LAN, o tecido da capa recebe a cor do jogador. As costuras de latão, a pele e a bolsa mantêm suas cores.

A [folha PNG editável usada no jogo](../Assets/Resources/Lumon/GuardianPainted.png) mede 3136 × 3976 px. Os quadros de 196 × 284 px formam dois blocos de oito colunas e catorze linhas. Cada bloco usa os rumos S, SE, L, NE, N, NO, O e SO. Leia primeiro as linhas do bloco esquerdo, depois as do direito. A pose 0 é repouso, 1–20 formam o ciclo e 21–26 recolhem a passada sobre cada um dos dois pés. A última linha do bloco direito é reservada.

A animação parte de peças pintadas separadas para o traje, os braços e as pernas. A pintura do rosto permanece a mesma em todo o ciclo de uma direção. O joelho dobra entre o quadril e o tornozelo, o pé livre passa perto do chão e os braços acompanham a perna oposta. A bota de apoio recua no desenho pela distância que a personagem avança no mundo. O renderizador corrige o deslocamento dos pés entre dois quadros. A referência de projeção da arte é a câmera de jogo a 32°.

A caminhada atinge 1,50 m/s e percorre 1,38 m por ciclo completo. A aceleração inicial e a frenagem usam o mesmo cálculo no modo solo e no anfitrião LAN. Com movimento reduzido, a guardiã mantém a pose de repouso na direção em que caminha.

O Unity reimporta a folha quando ela é editada. Para reconstruir o relevo PLY a partir do PNG, execute `python Art/Gaussian/painted-guardian.py` com Pillow. Para recompor a animação a partir das peças em `Art/Concepts/Observatorio`, execute o mesmo script com `--export-from-concepts <destino.png>` e com NumPy e OpenCV instalados. O método Unity `Lumon.HubWorld.Editor.HubSplatBuilder.Build` compila a cena instalada para Windows.

## Estudo anterior de proporções e paleta

A Guardiã é uma ilustração 2D pintada em camadas de cor, com aparência de aprendiz de 16 a 18 anos, rosto mais curto, mandíbula suave, olhos expressivos, cabelo escuro preso, casaco azul-petróleo, capa ameixa e gola de linho. O fecho de latão e cristal turquesa identifica o traje na praça. O rosto recebe os detalhes mais finos. As dobras e costuras da roupa devem funcionar como massas quando a personagem ocupa cerca de 165 px na tela.

## Prova no Unity

- Abra **Lumon → Open 2D Guardian Review** no Unity 6000.3.27f1. Use WASD ou as setas em Play Mode para deslocar a pose no espaço da praça com a câmera fixa.
- [Captura em 1280×720](../Art/Guardian2D/review/unity-1280x720.png), com 165 px de altura e 54 px de largura visível.
- [Captura em 1920×1080](../Art/Guardian2D/review/unity-1920x1080.png), com 248 px de altura e 80 px de largura visível.
- [Recortes sem ampliação](../Art/Guardian2D/review/unity-1280x720-detail.png) em 720p e [1080p](../Art/Guardian2D/review/unity-1920x1080-detail.png). A gola e o broche mantêm a leitura nas duas resoluções. As botas se confundem com algumas áreas do piso em 720p. O escorço inicial está mais próximo de uma vista frontal alta do que dos 30° da câmera.
- A praça usa a referência sem inscrições do Observatório. O cenário continua servindo de prova para a arte e o espaço de caminhada. HUD, oclusão e interações terão implementação própria.
- Em Play Mode, a Guardiã vira em oito rumos e mantém a última orientação quando para. A troca usa oito poses de repouso. Os ciclos de passada ainda estão em produção.
- [Oito vistas em tamanho real](../Art/Guardian2D/review/idle-directions/unity-oito-rumos.png). Na praça, as poses mediram de 165 a 166 px e a linha de apoio variou um pixel em 720p. [Medições por direção](../Art/Guardian2D/review/idle-directions/metrics.json).

## Regras para as próximas poses

- Fixar altura, largura de ombros, volume do cabelo e âncora entre as solas no repouso. Na caminhada, medir o pé de apoio por quadro e conferir o contato com a sombra.
- Usar oito rumos e desenhar as diferenças naturais de frente, perfil e costas. Preservar o lado do broche, do bolso e das mechas assimétricas em todas as vistas.
- Manter o arquivo-fonte em alta resolução e preparar transparência com borda suave. Conferir câmera, alpha, cor e leitura em 720p antes de ampliar a folha de animação.
- Manter PT-BR, EN e ES no catálogo da interface. A textura da personagem e as placas de cenário ficam sem texto.

## Reprodução

A [fonte Higgsfield](../Art/Guardian2D/source/guardian-apprentice-idle-three-quarter.png) contém o recorte original. `Art/Guardian2D/prepare_sprite.py` deriva a textura, o ponto de apoio e a sombra de contato. O método Unity `Lumon.GuardianReview.GuardianSpriteReview.RefreshAndCapture` gera os PNGs. `Art/Guardian2D/measure_captures.py` calcula a área visível pela diferença entre as duas renderizações e grava [as medidas](../Art/Guardian2D/review/capture-metrics.json).

Para a prova de oito rumos, execute `Art/Guardian2D/prepare_idle_directions.py`, o método Unity `Lumon.GuardianReview.GuardianDirectionalReview.InstallAndVerify` e `Art/Guardian2D/compose_idle_review.py`. As fontes Higgsfield estão em `Art/Guardian2D/source`. O Unity exercita a mesma função de movimento do teclado e registra a sprite selecionada, a velocidade, a orientação ao parar e a rotação da câmera. As capturas mostram poses de repouso. A inspeção das passadas, a oclusão no cenário e os testes com controle real permanecem abertos.
