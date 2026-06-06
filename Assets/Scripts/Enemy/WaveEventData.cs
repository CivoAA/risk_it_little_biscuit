using UnityEngine;

public static class WaveEventData
{
    public static TimeWaveManager.WaveEvent[] GetWaveEvents(
        // Wave 1
        GameObject marshmello,
        GameObject eliteMarshmello,
        GameObject evilSlime,
        GameObject mausMitMesser,

        // Wave 2
        GameObject saureMilch,
        GameObject miniMilch,
        GameObject muffin,

        // Wave 3
        GameObject suppe,
        GameObject pancake,
        GameObject fetti,

        GameObject[] slimeVariants, // erwartet genau 10 Prefabs

        // MiniBosse
        GameObject messerMaus1,
        GameObject messerMaus2,

        // Endboss
        GameObject keksKoenig,

        GameObject miniBoss_marshmello,
        GameObject blocker
    )
    {
        return new TimeWaveManager.WaveEvent[]
        {

            // ===== Wave 1 (0–5min) =====
            // Phase 1 – Start Gegner
            new TimeWaveManager.WaveEvent { triggerTime = 0f, enemyPrefab = marshmello, spawnCount = 20, spawnInterval = 1.5f },
            new TimeWaveManager.WaveEvent { triggerTime = 30f, enemyPrefab = marshmello, spawnCount = 20, spawnInterval = 1.2f },

            // Phase 2 – Evil Slime dazu
            new TimeWaveManager.WaveEvent { triggerTime = 45f, enemyPrefab = evilSlime, spawnCount = 5, spawnInterval = 2f },

            // Phase 3 – Marshmello + Evil Mix
            new TimeWaveManager.WaveEvent { triggerTime = 55f, enemyPrefab = marshmello, spawnCount = 15, spawnInterval = 0.7f },
            new TimeWaveManager.WaveEvent { triggerTime = 60f, enemyPrefab = miniBoss_marshmello, spawnCount = 1, spawnInterval = 1.5f },
            new TimeWaveManager.WaveEvent { triggerTime = 65f, enemyPrefab = evilSlime, spawnCount = 5, spawnInterval = 1.5f },

            // Phase 4 – Elite Marshmello kommt vereinzelt
            new TimeWaveManager.WaveEvent { triggerTime = 80f, enemyPrefab = eliteMarshmello, spawnCount = 1, spawnInterval = 0.1f },
            new TimeWaveManager.WaveEvent { triggerTime = 90f, enemyPrefab = marshmello, spawnCount = 35, spawnInterval = 0.6f },
            new TimeWaveManager.WaveEvent { triggerTime = 90f, enemyPrefab = evilSlime, spawnCount = 10, spawnInterval = 1f },
            //new TimeWaveManager.WaveEvent { triggerTime = 100f, enemyPrefab = eliteMarshmello, spawnCount = 1, spawnInterval = 0.1f },
            new TimeWaveManager.WaveEvent { triggerTime = 100f, enemyPrefab = mausMitMesser, spawnCount = 4, spawnInterval = 1.5f },

            // Phase 5 – Marschmello-Massenspawn
            new TimeWaveManager.WaveEvent { triggerTime = 120f, enemyPrefab = marshmello, spawnCount = 35, spawnInterval = 0.2f },
            new TimeWaveManager.WaveEvent { triggerTime = 121f, enemyPrefab = miniBoss_marshmello, spawnCount = 1, spawnInterval = 1.5f },

            // Phase 6 – Mix
            new TimeWaveManager.WaveEvent { triggerTime = 135f, enemyPrefab = marshmello, spawnCount = 35, spawnInterval = 0.2f },
            new TimeWaveManager.WaveEvent { triggerTime = 130f, enemyPrefab = evilSlime, spawnCount = 25, spawnInterval = 1f },

            // Phase 7 – Mehr Elite
            new TimeWaveManager.WaveEvent { triggerTime = 145f, enemyPrefab = eliteMarshmello, spawnCount = 4, spawnInterval = 2f },

            // Phase 8 – Maus mit Messer Einführung
            new TimeWaveManager.WaveEvent { triggerTime = 150f, enemyPrefab = mausMitMesser, spawnCount = 10, spawnInterval = 1f },
            new TimeWaveManager.WaveEvent { triggerTime = 150f, enemyPrefab = marshmello, spawnCount = 130, spawnInterval = 0.7f },
            new TimeWaveManager.WaveEvent { triggerTime = 160f, enemyPrefab = evilSlime, spawnCount = 40, spawnInterval = 0.8f },

            // Phase 9 – Maus Mix
            new TimeWaveManager.WaveEvent { triggerTime = 180f, enemyPrefab = mausMitMesser, spawnCount = 10, spawnInterval = 1f },
            new TimeWaveManager.WaveEvent { triggerTime = 181f, enemyPrefab = miniBoss_marshmello, spawnCount = 1, spawnInterval = 1.5f },
            new TimeWaveManager.WaveEvent { triggerTime = 185f, enemyPrefab = evilSlime, spawnCount = 40, spawnInterval = 0.8f },

            // Phase 10 – Letzter Push vor Boss
            new TimeWaveManager.WaveEvent { triggerTime = 210f, enemyPrefab = eliteMarshmello, spawnCount = 10, spawnInterval = 1f },
            new TimeWaveManager.WaveEvent { triggerTime = 200f, enemyPrefab = mausMitMesser, spawnCount = 20, spawnInterval = 0.8f },
            new TimeWaveManager.WaveEvent { triggerTime = 200f, enemyPrefab = evilSlime, spawnCount = 60, spawnInterval = 0.8f },

            new TimeWaveManager.WaveEvent { triggerTime = 240f, enemyPrefab = evilSlime, spawnCount = 60, spawnInterval = 0.6f },
            new TimeWaveManager.WaveEvent { triggerTime = 250f, enemyPrefab = eliteMarshmello, spawnCount = 10, spawnInterval = 1f },
            new TimeWaveManager.WaveEvent { triggerTime = 250f, enemyPrefab = mausMitMesser, spawnCount = 40, spawnInterval = 0.8f },

            new TimeWaveManager.WaveEvent { triggerTime = 270f, enemyPrefab = eliteMarshmello, spawnCount = 10, spawnInterval = 1f },
            // === MiniBoss: MesserMaus ===
            new TimeWaveManager.WaveEvent { triggerTime = 300f, enemyPrefab = messerMaus1, spawnCount = 1 },
            


            // ===== Wave 2 (5–10min) =====
            // Phase 1 – Start Gegner
            new TimeWaveManager.WaveEvent { triggerTime = 330f, enemyPrefab = eliteMarshmello, spawnCount = 40, spawnInterval = 1f },

            // Phase 2 – Elite Marshmello dazu (war vorher EvilSlime)
            new TimeWaveManager.WaveEvent { triggerTime = 340f, enemyPrefab = miniMilch, spawnCount = 6, spawnInterval = 2f },

            // Phase 3 – miniMilch + Elite Mix
            new TimeWaveManager.WaveEvent { triggerTime = 360f, enemyPrefab = eliteMarshmello, spawnCount = 40, spawnInterval = 0.7f },
            new TimeWaveManager.WaveEvent { triggerTime = 362f, enemyPrefab = miniMilch, spawnCount = 10, spawnInterval = 1.5f },

            // Phase 4 – Cupcake kommt vereinzelt (war vorher Elite Marshmello)
            new TimeWaveManager.WaveEvent { triggerTime = 380f, enemyPrefab = muffin, spawnCount = 1, spawnInterval = 0.1f },
            new TimeWaveManager.WaveEvent { triggerTime = 390f, enemyPrefab = eliteMarshmello, spawnCount = 50, spawnInterval = 0.6f },
            new TimeWaveManager.WaveEvent { triggerTime = 390f, enemyPrefab = miniMilch, spawnCount = 15, spawnInterval = 1f },
            new TimeWaveManager.WaveEvent { triggerTime = 400f, enemyPrefab = muffin, spawnCount = 1, spawnInterval = 0.1f },
            new TimeWaveManager.WaveEvent { triggerTime = 400f, enemyPrefab = saureMilch, spawnCount = 4, spawnInterval = 1.5f },

            // Phase 5 – miniMilch-Massenspawn
            new TimeWaveManager.WaveEvent { triggerTime = 420f, enemyPrefab = eliteMarshmello, spawnCount = 50, spawnInterval = 0.2f },

            // Phase 6 – Mix
            new TimeWaveManager.WaveEvent { triggerTime = 435f, enemyPrefab = eliteMarshmello, spawnCount = 50, spawnInterval = 0.2f },
            new TimeWaveManager.WaveEvent { triggerTime = 430f, enemyPrefab = miniMilch, spawnCount = 25, spawnInterval = 1f },

            // Phase 7 – Mehr Cupcakes
            new TimeWaveManager.WaveEvent { triggerTime = 445f, enemyPrefab = muffin, spawnCount = 4, spawnInterval = 2f },

            // Phase 8 – Saure Milch Einführung (wie MausMitMesser)
            new TimeWaveManager.WaveEvent { triggerTime = 450f, enemyPrefab = saureMilch, spawnCount = 10, spawnInterval = 1f },
            new TimeWaveManager.WaveEvent { triggerTime = 450f, enemyPrefab = eliteMarshmello, spawnCount = 150, spawnInterval = 0.7f },
            new TimeWaveManager.WaveEvent { triggerTime = 460f, enemyPrefab = miniMilch, spawnCount = 40, spawnInterval = 0.8f },

            // Phase 9 – Saure Milch Mix
            new TimeWaveManager.WaveEvent { triggerTime = 480f, enemyPrefab = saureMilch, spawnCount = 10, spawnInterval = 1f },
            new TimeWaveManager.WaveEvent { triggerTime = 485f, enemyPrefab = miniMilch, spawnCount = 40, spawnInterval = 0.8f },

            // Phase 10 – Letzter Push vor Boss
            new TimeWaveManager.WaveEvent { triggerTime = 510f, enemyPrefab = muffin, spawnCount = 10, spawnInterval = 1f },
            new TimeWaveManager.WaveEvent { triggerTime = 500f, enemyPrefab = saureMilch, spawnCount = 20, spawnInterval = 0.8f },
            new TimeWaveManager.WaveEvent { triggerTime = 500f, enemyPrefab = miniMilch, spawnCount = 60, spawnInterval = 0.8f },

            new TimeWaveManager.WaveEvent { triggerTime = 540f, enemyPrefab = miniMilch, spawnCount = 60, spawnInterval = 0.6f },
            new TimeWaveManager.WaveEvent { triggerTime = 550f, enemyPrefab = muffin, spawnCount = 10, spawnInterval = 1f },
            new TimeWaveManager.WaveEvent { triggerTime = 550f, enemyPrefab = saureMilch, spawnCount = 40, spawnInterval = 0.8f },

            new TimeWaveManager.WaveEvent { triggerTime = 570f, enemyPrefab = muffin, spawnCount = 10, spawnInterval = 1f },

            // === MiniBoss: MesserRatte ===
            new TimeWaveManager.WaveEvent { triggerTime = 600f, enemyPrefab = messerMaus2, spawnCount = 1 },


            // ===== Wave 3 (10–15min) =====
            // Phase 1 – Start Gegner
            new TimeWaveManager.WaveEvent { triggerTime = 600f, enemyPrefab = muffin, spawnCount = 35, spawnInterval = 1f },
            new TimeWaveManager.WaveEvent { triggerTime = 620f, enemyPrefab = muffin, spawnCount = 40, spawnInterval = 0.9f },

            // Phase 2 – Pancake dazu (war vorher eliteMarshmello)
            new TimeWaveManager.WaveEvent { triggerTime = 640f, enemyPrefab = pancake, spawnCount = 10, spawnInterval = 2f },

            // Phase 3 – Muffin + Pancake Mix
            new TimeWaveManager.WaveEvent { triggerTime = 660f, enemyPrefab = muffin, spawnCount = 40, spawnInterval = 0.7f },
            new TimeWaveManager.WaveEvent { triggerTime = 662f, enemyPrefab = pancake, spawnCount = 10, spawnInterval = 1.5f },

            // Phase 4 – Suppe kommt vereinzelt (war vorher cupcake)
            new TimeWaveManager.WaveEvent { triggerTime = 680f, enemyPrefab = suppe, spawnCount = 1, spawnInterval = 0.1f },
            new TimeWaveManager.WaveEvent { triggerTime = 690f, enemyPrefab = muffin, spawnCount = 50, spawnInterval = 0.6f },
            new TimeWaveManager.WaveEvent { triggerTime = 690f, enemyPrefab = pancake, spawnCount = 15, spawnInterval = 1f },
            new TimeWaveManager.WaveEvent { triggerTime = 700f, enemyPrefab = suppe, spawnCount = 1, spawnInterval = 0.1f },
            new TimeWaveManager.WaveEvent { triggerTime = 700f, enemyPrefab = fetti, spawnCount = 4, spawnInterval = 1.5f },

            // Phase 5 – Muffin-Massenspawn
            new TimeWaveManager.WaveEvent { triggerTime = 720f, enemyPrefab = muffin, spawnCount = 50, spawnInterval = 0.2f },

            // Phase 6 – Mix
            new TimeWaveManager.WaveEvent { triggerTime = 735f, enemyPrefab = muffin, spawnCount = 50, spawnInterval = 0.2f },
            new TimeWaveManager.WaveEvent { triggerTime = 730f, enemyPrefab = pancake, spawnCount = 25, spawnInterval = 1f },

            // Phase 7 – Mehr Suppe
            new TimeWaveManager.WaveEvent { triggerTime = 745f, enemyPrefab = suppe, spawnCount = 4, spawnInterval = 2f },

            // Phase 8 – Fetti als Druck-Gegner
            new TimeWaveManager.WaveEvent { triggerTime = 750f, enemyPrefab = fetti, spawnCount = 10, spawnInterval = 1f },
            new TimeWaveManager.WaveEvent { triggerTime = 750f, enemyPrefab = muffin, spawnCount = 150, spawnInterval = 0.7f },
            new TimeWaveManager.WaveEvent { triggerTime = 760f, enemyPrefab = pancake, spawnCount = 40, spawnInterval = 0.8f },

            // Phase 9 – Fetti Mix
            new TimeWaveManager.WaveEvent { triggerTime = 780f, enemyPrefab = fetti, spawnCount = 10, spawnInterval = 1f },
            new TimeWaveManager.WaveEvent { triggerTime = 785f, enemyPrefab = pancake, spawnCount = 40, spawnInterval = 0.8f },

            // Phase 10 – Letzter Push vor Slime-Welle
            new TimeWaveManager.WaveEvent { triggerTime = 810f, enemyPrefab = suppe, spawnCount = 10, spawnInterval = 1f },
            new TimeWaveManager.WaveEvent { triggerTime = 800f, enemyPrefab = fetti, spawnCount = 20, spawnInterval = 0.8f },
            new TimeWaveManager.WaveEvent { triggerTime = 800f, enemyPrefab = pancake, spawnCount = 60, spawnInterval = 0.8f },

            new TimeWaveManager.WaveEvent { triggerTime = 840f, enemyPrefab = pancake, spawnCount = 60, spawnInterval = 0.6f },
            new TimeWaveManager.WaveEvent { triggerTime = 850f, enemyPrefab = suppe, spawnCount = 10, spawnInterval = 1f },
            new TimeWaveManager.WaveEvent { triggerTime = 850f, enemyPrefab = fetti, spawnCount = 40, spawnInterval = 0.8f },

            new TimeWaveManager.WaveEvent { triggerTime = 870f, enemyPrefab = suppe, spawnCount = 10, spawnInterval = 1f },

            // ===== Endboss =====
            new TimeWaveManager.WaveEvent {
                triggerTime = 890f,
                enemyPrefab = keksKoenig,
                spawnCount = 1
            },

            // ===== Slime Loop (1 Slime pro Sekunde, rotierend) =====

            // Slime 0
            new TimeWaveManager.WaveEvent { triggerTime = 890f, enemyPrefab = slimeVariants[0], spawnCount = 48, spawnInterval = 10f },

            // Slime 1
            new TimeWaveManager.WaveEvent { triggerTime = 891f, enemyPrefab = slimeVariants[1], spawnCount = 48, spawnInterval = 10f },

            // Slime 2
            new TimeWaveManager.WaveEvent { triggerTime = 892f, enemyPrefab = slimeVariants[2], spawnCount = 48, spawnInterval = 10f },

            // Slime 3
            new TimeWaveManager.WaveEvent { triggerTime = 893f, enemyPrefab = slimeVariants[3], spawnCount = 48, spawnInterval = 10f },

            // Slime 4
            new TimeWaveManager.WaveEvent { triggerTime = 894f, enemyPrefab = slimeVariants[4], spawnCount = 48, spawnInterval = 10f },

            // Slime 5
            new TimeWaveManager.WaveEvent { triggerTime = 895f, enemyPrefab = slimeVariants[5], spawnCount = 48, spawnInterval = 10f },

            // Slime 6
            new TimeWaveManager.WaveEvent { triggerTime = 896f, enemyPrefab = slimeVariants[6], spawnCount = 48, spawnInterval = 10f },

            // Slime 7
            new TimeWaveManager.WaveEvent { triggerTime = 897f, enemyPrefab = slimeVariants[7], spawnCount = 48, spawnInterval = 10f },

            // Slime 8
            new TimeWaveManager.WaveEvent { triggerTime = 898f, enemyPrefab = slimeVariants[8], spawnCount = 48, spawnInterval = 10f },

            // Slime 9
            new TimeWaveManager.WaveEvent { triggerTime = 899f, enemyPrefab = slimeVariants[9], spawnCount = 48, spawnInterval = 10f }

        };
    }
}
