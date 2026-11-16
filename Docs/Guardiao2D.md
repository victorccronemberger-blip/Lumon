# Guardiã do Observatório · direção de arte 2D

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
