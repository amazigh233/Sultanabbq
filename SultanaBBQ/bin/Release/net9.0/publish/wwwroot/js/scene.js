// ============================================
// SULTANA BBQ — Interactive 3D Menu Scene v2
// ============================================

window.SultanaScene = (() => {
    let scene, camera, renderer, controls;
    let foodItems = [];
    let raycaster, mouse;
    let blazorRef = null;
    let containerId = null;
    let animationId = null;
    let fireParticles = null;
    let smokeParticles = null;
    let emberParticles = null;
    let fireLights = [];
    let time = 0;
    let hoveredItem = null;

    // Materials reused across models
    const MAT = {};

    const FOOD_CATEGORIES = [
        { name: 'Grillgerechten',     color: 0xB5451B, position: { x:  0,    y: 1.6, z:  0   }, shape: 'mixed'    },
        { name: 'Kebab',              color: 0xC8832A, position: { x: -2.8,  y: 1.4, z:  0.8  }, shape: 'kebab'    },
        { name: 'Falafel',            color: 0x7A8C30, position: { x:  2.8,  y: 1.2, z:  0.8  }, shape: 'falafel'  },
        { name: 'Hamburgers',         color: 0x7A3B10, position: { x: -1.8,  y: 1.4, z: -2.2  }, shape: 'burger'   },
        { name: 'Wraps',              color: 0xD4B483, position: { x:  1.8,  y: 1.3, z: -2.2  }, shape: 'wrap'     },
        { name: 'Koude Voorgerechten',color: 0x5A7A22, position: { x: -3.6,  y: 1.0, z: -1.0  }, shape: 'bowl'     },
        { name: 'Snacks',             color: 0xD4A020, position: { x:  3.6,  y: 1.1, z: -1.0  }, shape: 'fries'    },
        { name: 'Warme Voorgerechten',color: 0xBB6B2A, position: { x:  0,    y: 1.2, z:  2.8  }, shape: 'plate'    },
        { name: 'Dranken',            color: 0x6ABADC, position: { x:  3.8,  y: 1.4, z:  2.0  }, shape: 'glass'    },
    ];

    // ─── Init ───────────────────────────────────────────────────────────────
    function init(containerElementId, dotNetRef) {
        containerId = containerElementId;
        blazorRef   = dotNetRef;
        const container = document.getElementById(containerId);
        if (!container) return;

        // Scene
        scene = new THREE.Scene();
        scene.fog = new THREE.FogExp2(0x0A0A0A, 0.055);
        scene.background = new THREE.Color(0x0A0A0A);

        // Camera
        camera = new THREE.PerspectiveCamera(42, container.clientWidth / container.clientHeight, 0.1, 120);
        camera.position.set(0, 6.5, 12);
        camera.lookAt(0, 0.5, 0);

        // Renderer
        renderer = new THREE.WebGLRenderer({ antialias: true });
        renderer.setSize(container.clientWidth, container.clientHeight);
        renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
        renderer.shadowMap.enabled = true;
        renderer.shadowMap.type    = THREE.PCFSoftShadowMap;
        renderer.toneMapping       = THREE.ACESFilmicToneMapping;
        renderer.toneMappingExposure = 1.1;
        container.appendChild(renderer.domElement);

        // Controls
        controls = new THREE.OrbitControls(camera, renderer.domElement);
        controls.enableDamping    = true;
        controls.dampingFactor    = 0.06;
        controls.maxPolarAngle    = Math.PI / 2.05;
        controls.minPolarAngle    = Math.PI / 8;
        controls.maxDistance      = 17;
        controls.minDistance      = 5;
        controls.enablePan        = false;
        controls.enableZoom       = false;
        controls.autoRotate       = true;

        // On touch devices disable drag controls so single-finger page scroll works
        if ('ontouchstart' in window) {
            controls.enableRotate = false;
        }
        controls.autoRotateSpeed  = 0.4;
        controls.target.set(0, 0.5, 0);

        // Raycaster
        raycaster = new THREE.Raycaster();
        mouse     = new THREE.Vector2();

        // Build shared materials
        buildMaterials();

        // Scene objects
        setupLighting();
        createGround();
        createGrill();
        createFoodItems();
        createFireParticles();
        createSmokeParticles();
        createEmberParticles();

        // Events
        container.addEventListener('click',     onMouseClick);
        container.addEventListener('mousemove', onMouseMove);
        window.addEventListener('resize',       onResize);

        animate();
    }

    // ─── Materials ───────────────────────────────────────────────────────────
    function buildMaterials() {
        MAT.metal = new THREE.MeshStandardMaterial({ color: 0x222222, metalness: 0.9, roughness: 0.25 });
        MAT.metalGold = new THREE.MeshStandardMaterial({ color: 0xC8922A, metalness: 0.95, roughness: 0.15 });
        MAT.coal = new THREE.MeshStandardMaterial({ color: 0x1A1A1A, roughness: 1.0 });
        MAT.ember = new THREE.MeshStandardMaterial({ color: 0xFF3300, emissive: 0xFF2200, emissiveIntensity: 1.2, roughness: 1.0 });
        MAT.ground = new THREE.MeshStandardMaterial({ color: 0x0D0D0D, roughness: 1.0 });
    }

    // ─── Lighting ────────────────────────────────────────────────────────────
    function setupLighting() {
        scene.add(new THREE.AmbientLight(0x1A1208, 1.5));

        // Main warm key light
        const key = new THREE.DirectionalLight(0xFFE4AA, 1.2);
        key.position.set(4, 10, 6);
        key.castShadow = true;
        key.shadow.mapSize.set(2048, 2048);
        key.shadow.camera.near = 0.5;
        key.shadow.camera.far  = 40;
        key.shadow.camera.left = -10;
        key.shadow.camera.right = 10;
        key.shadow.camera.top = 10;
        key.shadow.camera.bottom = -10;
        key.shadow.bias = -0.002;
        scene.add(key);

        // Cold fill from opposite side
        const fill = new THREE.DirectionalLight(0x6688AA, 0.3);
        fill.position.set(-6, 4, -5);
        scene.add(fill);

        // Three dynamic fire lights that flicker
        const firePositions = [
            { x:  0.8, y: 0.5, z:  0.5 },
            { x: -0.8, y: 0.4, z: -0.5 },
            { x:  0.2, y: 0.6, z: -0.8 },
        ];
        firePositions.forEach(pos => {
            const fl = new THREE.PointLight(0xFF5500, 3.5, 7);
            fl.position.set(pos.x, pos.y, pos.z);
            scene.add(fl);
            fireLights.push({ light: fl, baseIntensity: 3.5, offset: Math.random() * Math.PI * 2 });
        });

        // Gold rim
        const rim = new THREE.PointLight(0xD4A853, 1.2, 14);
        rim.position.set(5, 4, 4);
        scene.add(rim);

        // Red accent
        const accent = new THREE.PointLight(0xC44536, 0.6, 12);
        accent.position.set(-5, 3, -5);
        scene.add(accent);
    }

    // ─── Ground ──────────────────────────────────────────────────────────────
    function createGround() {
        const ground = new THREE.Mesh(
            new THREE.PlaneGeometry(60, 60),
            MAT.ground
        );
        ground.rotation.x = -Math.PI / 2;
        ground.position.y = -0.52;
        ground.receiveShadow = true;
        scene.add(ground);
    }

    // ─── Grill ───────────────────────────────────────────────────────────────
    function createGrill() {
        const grillGroup = new THREE.Group();

        // Outer bowl (deep kettle shape)
        const bowlGeo = new THREE.SphereGeometry(4.4, 48, 32, 0, Math.PI * 2, 0, Math.PI * 0.55);
        const bowlMat = new THREE.MeshStandardMaterial({ color: 0x181818, metalness: 0.7, roughness: 0.4, side: THREE.DoubleSide });
        const bowl = new THREE.Mesh(bowlGeo, bowlMat);
        bowl.rotation.x = Math.PI;
        bowl.position.y = -0.1;
        bowl.receiveShadow = true;
        grillGroup.add(bowl);

        // Inner coal bed (flat disk with emissive glow)
        const coalBase = new THREE.Mesh(
            new THREE.CylinderGeometry(3.5, 3.5, 0.12, 48),
            new THREE.MeshStandardMaterial({ color: 0x111111, roughness: 1.0, emissive: 0x220800, emissiveIntensity: 0.4 })
        );
        coalBase.position.y = -0.38;
        coalBase.receiveShadow = true;
        grillGroup.add(coalBase);

        // Scattered coal lumps
        for (let i = 0; i < 30; i++) {
            const r = Math.random() * 3.0;
            const a = Math.random() * Math.PI * 2;
            const coal = new THREE.Mesh(
                new THREE.DodecahedronGeometry(0.12 + Math.random() * 0.14, 0),
                Math.random() > 0.35 ? MAT.ember : MAT.coal
            );
            coal.position.set(Math.cos(a) * r, -0.3 + Math.random() * 0.08, Math.sin(a) * r);
            coal.rotation.set(Math.random() * Math.PI, Math.random() * Math.PI, Math.random() * Math.PI);
            grillGroup.add(coal);
        }

        // Grate — two layers of perpendicular bars
        const grateMat = new THREE.MeshStandardMaterial({ color: 0x303030, metalness: 0.85, roughness: 0.3 });
        const grateGroup = new THREE.Group();
        for (let i = -3.2; i <= 3.2; i += 0.45) {
            const barH = new THREE.Mesh(new THREE.CylinderGeometry(0.035, 0.035, 7.2, 8), grateMat);
            barH.rotation.z = Math.PI / 2;
            barH.position.set(0, 0, i);
            grateGroup.add(barH);

            const barV = new THREE.Mesh(new THREE.CylinderGeometry(0.035, 0.035, 7.2, 8), grateMat);
            barV.position.set(i, 0, 0);
            grateGroup.add(barV);
        }
        // Clip grate to circle
        grateGroup.position.y = 0.18;
        grillGroup.add(grateGroup);

        // Gold rim ring
        const rim = new THREE.Mesh(
            new THREE.TorusGeometry(4.3, 0.13, 12, 80),
            MAT.metalGold
        );
        rim.rotation.x = Math.PI / 2;
        rim.position.y = 0.22;
        grillGroup.add(rim);

        // Three legs
        for (let i = 0; i < 3; i++) {
            const angle = (i / 3) * Math.PI * 2;
            const leg = new THREE.Mesh(
                new THREE.CylinderGeometry(0.07, 0.05, 3.0, 8),
                MAT.metal
            );
            leg.position.set(Math.cos(angle) * 3.8, -1.8, Math.sin(angle) * 3.8);
            leg.rotation.z = Math.sin(angle) * 0.22;
            leg.rotation.x = -Math.cos(angle) * 0.22;
            leg.castShadow = true;
            grillGroup.add(leg);

            // Foot
            const foot = new THREE.Mesh(new THREE.CylinderGeometry(0.12, 0.12, 0.08, 12), MAT.metal);
            foot.position.set(Math.cos(angle) * 4.3, -3.3, Math.sin(angle) * 4.3);
            grillGroup.add(foot);
        }

        scene.add(grillGroup);
    }

    // ─── Food Items ──────────────────────────────────────────────────────────
    function createFoodItems() {
        FOOD_CATEGORIES.forEach((item, index) => {
            const group = new THREE.Group();

            switch (item.shape) {
                case 'mixed':   buildMixedGrill(group, item.color);  break;
                case 'kebab':   buildKebab(group, item.color);       break;
                case 'falafel': buildFalafel(group, item.color);     break;
                case 'burger':  buildBurger(group, item.color);      break;
                case 'wrap':    buildWrap(group, item.color);        break;
                case 'bowl':    buildBowl(group, item.color);        break;
                case 'fries':   buildFries(group, item.color);       break;
                case 'plate':   buildPlate(group, item.color);       break;
                case 'glass':   buildGlass(group, item.color);       break;
            }

            // Gold glow ring on grate
            const ring = new THREE.Mesh(
                new THREE.RingGeometry(0.42, 0.68, 40),
                new THREE.MeshBasicMaterial({ color: item.color, transparent: true, opacity: 0.25, side: THREE.DoubleSide, depthWrite: false })
            );
            ring.rotation.x = -Math.PI / 2;
            ring.position.y = -0.25;
            group.add(ring);

            // Floating label
            group.add(createLabel(item.name));

            group.position.set(item.position.x, item.position.y, item.position.z);
            group.userData = { category: item.name, index, baseY: item.position.y };
            group.castShadow = true;

            scene.add(group);
            foodItems.push(group);
        });
    }

    // ─── Food Builders ───────────────────────────────────────────────────────

    function mat(color, rough, metal, emissive, emissiveI) {
        return new THREE.MeshStandardMaterial({
            color:            color,
            roughness:        rough  ?? 0.65,
            metalness:        metal  ?? 0.0,
            emissive:         emissive  ? new THREE.Color(emissive) : undefined,
            emissiveIntensity: emissiveI ?? 0,
        });
    }

    function buildMixedGrill(g, color) {
        const skewMat = mat(0x9C6B3C, 0.55, 0.3);
        const meatMat = mat(color,    0.55, 0.0);
        const charMat = mat(0x2A1A0A, 0.9,  0.0);

        for (let s = -0.55; s <= 0.55; s += 0.55) {
            // Skewer rod
            const rod = new THREE.Mesh(new THREE.CylinderGeometry(0.028, 0.022, 2.4, 8), skewMat);
            rod.rotation.z = Math.PI / 2;
            rod.position.z = s;
            g.add(rod);

            // Meat chunks along skewer
            for (let j = -0.75; j <= 0.75; j += 0.38) {
                const size = 0.18 + Math.random() * 0.06;
                const chunk = new THREE.Mesh(
                    new THREE.BoxGeometry(size * 1.4, size, size),
                    Math.random() > 0.7 ? charMat : meatMat
                );
                chunk.position.set(j, (Math.random() - 0.5) * 0.06, s);
                chunk.rotation.set(0, Math.random() * 0.4 - 0.2, Math.random() * 0.2);
                g.add(chunk);
            }
        }

        // Grilled vegetables between skewers
        const vegMat = mat(0x3A7A1A, 0.8);
        const pepMat = mat(0xCC3300, 0.7);
        [[-0.28, 0.1, 0], [0.28, 0.1, 0]].forEach(([x, y, z]) => {
            const veg = new THREE.Mesh(new THREE.BoxGeometry(0.14, 0.12, 0.14), Math.random() > 0.5 ? vegMat : pepMat);
            veg.position.set(x, y, z);
            veg.rotation.y = Math.random();
            g.add(veg);
        });
    }

    function buildKebab(g, color) {
        // Vertical rotating döner cone — layered slices
        const coneBase = new THREE.Mesh(
            new THREE.CylinderGeometry(0.28, 0.42, 1.6, 20),
            mat(color, 0.55)
        );
        g.add(coneBase);

        // Layered rings for texture
        for (let y = -0.65; y <= 0.65; y += 0.2) {
            const ring = new THREE.Mesh(
                new THREE.TorusGeometry(0.32 - y * 0.06, 0.04, 6, 24),
                mat(y > 0.3 ? 0xC86030 : color, 0.7)
            );
            ring.position.y = y;
            g.add(ring);
        }

        // Skewer spike
        const spike = new THREE.Mesh(
            new THREE.CylinderGeometry(0.025, 0.015, 2.2, 8),
            mat(0xAAAAAA, 0.2, 0.9)
        );
        g.add(spike);

        // Decorative top
        const cap = new THREE.Mesh(
            new THREE.SphereGeometry(0.28, 16, 10, 0, Math.PI * 2, 0, Math.PI * 0.55),
            mat(0xC88030, 0.45)
        );
        cap.position.y = 0.8;
        g.add(cap);
    }

    function buildFalafel(g, color) {
        const positions = [
            [ 0,    0,    0   ],
            [-0.32, 0,    0.28],
            [ 0.32, 0,    0.28],
            [ 0,    0,   -0.34],
            [ 0.16, 0.3,  0.12],
        ];
        const sesame = mat(0xE8D8A0, 0.9);
        positions.forEach(([x, y, z], i) => {
            // Ball
            const ball = new THREE.Mesh(
                new THREE.SphereGeometry(0.19, 20, 20),
                mat(color, 0.75)
            );
            ball.position.set(x, y, z);
            g.add(ball);

            // Sesame specks
            for (let s = 0; s < 5; s++) {
                const speck = new THREE.Mesh(new THREE.SphereGeometry(0.015, 4, 4), sesame);
                const theta = Math.random() * Math.PI * 2;
                const phi   = Math.random() * Math.PI;
                speck.position.set(
                    x + 0.19 * Math.sin(phi) * Math.cos(theta),
                    y + 0.19 * Math.cos(phi),
                    z + 0.19 * Math.sin(phi) * Math.sin(theta)
                );
                g.add(speck);
            }
        });

        // Tahini drizzle suggestion
        const sauceGeo = new THREE.CylinderGeometry(0.28, 0.26, 0.04, 24);
        const sauce = new THREE.Mesh(sauceGeo, mat(0xF0E0A0, 0.5));
        sauce.position.y = -0.22;
        g.add(sauce);
    }

    function buildBurger(g, color) {
        const bunMat    = mat(0xD4A050, 0.8);
        const bunTopMat = mat(0xC89040, 0.75);
        const pattyMat  = mat(color,    0.55);
        const cheeseMat = mat(0xFFCC33, 0.6);
        const lettMat   = mat(0x3DAA22, 0.9);
        const tomMat    = mat(0xCC2211, 0.7);
        const sauceMat  = mat(0xDD4411, 0.8);

        // Bottom bun
        const bunB = new THREE.Mesh(new THREE.CylinderGeometry(0.46, 0.5, 0.18, 24), bunMat);
        bunB.position.y = -0.28;
        g.add(bunB);

        // Sauce on bottom bun
        const sauce = new THREE.Mesh(new THREE.CylinderGeometry(0.43, 0.43, 0.03, 20), sauceMat);
        sauce.position.y = -0.16;
        g.add(sauce);

        // Lettuce (slightly ruffled disk)
        const lett = new THREE.Mesh(new THREE.CylinderGeometry(0.52, 0.48, 0.04, 24), lettMat);
        lett.position.y = -0.1;
        g.add(lett);

        // Tomato slice
        const tom = new THREE.Mesh(new THREE.CylinderGeometry(0.45, 0.45, 0.055, 20), tomMat);
        tom.position.y = -0.03;
        g.add(tom);

        // Patty
        const patty = new THREE.Mesh(new THREE.CylinderGeometry(0.44, 0.46, 0.15, 24), pattyMat);
        patty.position.y = 0.1;
        g.add(patty);

        // Cheese slice (slightly overhanging)
        const cheese = new THREE.Mesh(new THREE.BoxGeometry(0.98, 0.04, 0.98), cheeseMat);
        cheese.position.y = 0.2;
        cheese.rotation.y = Math.PI / 5;
        g.add(cheese);

        // Top bun (dome)
        const bunT = new THREE.Mesh(
            new THREE.SphereGeometry(0.46, 24, 18, 0, Math.PI * 2, 0, Math.PI * 0.52),
            bunTopMat
        );
        bunT.position.y = 0.27;
        g.add(bunT);

        // Sesame seeds on top bun
        const seedMat = mat(0xF0E090, 0.9);
        for (let i = 0; i < 8; i++) {
            const seed = new THREE.Mesh(new THREE.SphereGeometry(0.022, 5, 5), seedMat);
            const a = (i / 8) * Math.PI * 2 + Math.random() * 0.3;
            const r = 0.2 + Math.random() * 0.12;
            seed.position.set(Math.cos(a) * r, 0.53, Math.sin(a) * r);
            g.add(seed);
        }
    }

    function buildWrap(g, color) {
        // Flatbread cone
        const cone = new THREE.Mesh(
            new THREE.ConeGeometry(0.38, 1.4, 16, 4, true),
            mat(color, 0.75)
        );
        cone.rotation.z = -Math.PI / 2.5;
        cone.position.set(0.1, 0.1, 0);
        g.add(cone);

        // Visible filling layers at the open top
        const fillings = [
            { c: 0x6E3C10, y: 0.45, z:  0.05 }, // meat
            { c: 0x2A8020, y: 0.5,  z: -0.05 }, // salad
            { c: 0xF0E090, y: 0.42, z:  0.1  }, // sauce
            { c: 0xCC2211, y: 0.48, z:  0.0  }, // tomato
        ];
        fillings.forEach(({ c, y, z }) => {
            const fill = new THREE.Mesh(
                new THREE.SphereGeometry(0.14, 10, 8),
                mat(c, 0.8)
            );
            fill.scale.y = 0.6;
            fill.position.set(0.22, y, z);
            g.add(fill);
        });
    }

    function buildBowl(g, color) {
        // Bowl body (half-sphere)
        const bowl = new THREE.Mesh(
            new THREE.SphereGeometry(0.5, 28, 16, 0, Math.PI * 2, Math.PI * 0.5, Math.PI * 0.5),
            mat(0xF0ECE0, 0.35, 0.15)
        );
        bowl.rotation.x = Math.PI;
        bowl.position.y = -0.05;
        g.add(bowl);

        // Rim
        const rim = new THREE.Mesh(
            new THREE.TorusGeometry(0.5, 0.03, 8, 40),
            mat(0xE8E0D0, 0.3, 0.1)
        );
        rim.rotation.x = Math.PI / 2;
        rim.position.y = 0.01;
        g.add(rim);

        // Hummus/dip filling
        const fill = new THREE.Mesh(
            new THREE.CylinderGeometry(0.44, 0.38, 0.16, 28),
            mat(color, 0.75)
        );
        fill.position.y = 0.06;
        g.add(fill);

        // Olive oil drizzle (gold pool)
        const drizzle = new THREE.Mesh(
            new THREE.CircleGeometry(0.18, 20),
            mat(0xD4A030, 0.4)
        );
        drizzle.rotation.x = -Math.PI / 2;
        drizzle.position.y = 0.15;
        g.add(drizzle);

        // Paprika sprinkle
        for (let i = 0; i < 5; i++) {
            const s = new THREE.Mesh(new THREE.SphereGeometry(0.025, 5, 5), mat(0xCC3300, 0.8));
            const a = (i / 5) * Math.PI * 2;
            s.position.set(Math.cos(a) * 0.22, 0.16, Math.sin(a) * 0.22);
            g.add(s);
        }

        // Pita bread half
        const pita = new THREE.Mesh(
            new THREE.CylinderGeometry(0.3, 0.28, 0.06, 12, 1, false, 0, Math.PI),
            mat(0xE8C880, 0.8)
        );
        pita.position.set(0.38, 0.08, 0);
        pita.rotation.y = Math.PI / 6;
        g.add(pita);
    }

    function buildFries(g, color) {
        // Red container box
        const box = new THREE.Mesh(
            new THREE.BoxGeometry(0.55, 0.7, 0.4),
            mat(0xCC1111, 0.5)
        );
        g.add(box);

        // White stripe logo-like stripe
        const stripe = new THREE.Mesh(
            new THREE.BoxGeometry(0.56, 0.12, 0.02),
            mat(0xFFFFFF, 0.7)
        );
        stripe.position.set(0, 0.1, 0.21);
        g.add(stripe);

        // Fries bundle
        const fryMat = mat(color, 0.65);
        const darkFryMat = mat(0xBB8B20, 0.7); // slightly darker = more fried
        for (let i = 0; i < 12; i++) {
            const fry = new THREE.Mesh(
                new THREE.BoxGeometry(0.06, 0.45 + Math.random() * 0.2, 0.055),
                Math.random() > 0.3 ? fryMat : darkFryMat
            );
            fry.position.set(
                (Math.random() - 0.5) * 0.38,
                0.5 + Math.random() * 0.12,
                (Math.random() - 0.5) * 0.26
            );
            fry.rotation.set(
                (Math.random() - 0.5) * 0.35,
                Math.random() * Math.PI,
                (Math.random() - 0.5) * 0.25
            );
            g.add(fry);
        }

        // Sauce dip cup
        const cup = new THREE.Mesh(new THREE.CylinderGeometry(0.12, 0.1, 0.18, 14), mat(0xF5F5F5, 0.4));
        cup.position.set(0.5, -0.22, 0);
        g.add(cup);
        const sauce = new THREE.Mesh(new THREE.CylinderGeometry(0.11, 0.1, 0.08, 14), mat(0xEE3311, 0.6));
        sauce.position.set(0.5, -0.1, 0);
        g.add(sauce);
    }

    function buildPlate(g, color) {
        // Plate base
        const plate = new THREE.Mesh(
            new THREE.CylinderGeometry(0.58, 0.52, 0.07, 32),
            mat(0xF2EEE4, 0.3, 0.1)
        );
        g.add(plate);

        // Raised rim
        const rim = new THREE.Mesh(
            new THREE.TorusGeometry(0.56, 0.04, 8, 40),
            mat(0xE8E2D8, 0.35, 0.1)
        );
        rim.rotation.x = Math.PI / 2;
        rim.position.y = 0.07;
        g.add(rim);

        // Main item (kibbeh / samosa shape)
        const main = new THREE.Mesh(
            new THREE.SphereGeometry(0.18, 14, 10),
            mat(color, 0.6)
        );
        main.scale.set(1.3, 0.85, 1.0);
        main.position.set(0.14, 0.15, 0.05);
        g.add(main);

        // Second item
        const item2 = new THREE.Mesh(
            new THREE.ConeGeometry(0.12, 0.28, 8),
            mat(0xD4B060, 0.65)
        );
        item2.position.set(-0.18, 0.2, 0.1);
        item2.rotation.z = Math.PI / 2.5;
        g.add(item2);

        // Herb garnish (green dots)
        const herb = mat(0x22AA44, 0.9);
        for (let i = 0; i < 6; i++) {
            const h = new THREE.Mesh(new THREE.SphereGeometry(0.022, 5, 5), herb);
            const a = (i / 6) * Math.PI * 2;
            h.position.set(Math.cos(a) * 0.32, 0.1, Math.sin(a) * 0.32);
            g.add(h);
        }

        // Sauce dot
        const sauce = new THREE.Mesh(new THREE.CylinderGeometry(0.1, 0.09, 0.03, 20), mat(0xCC8830, 0.6));
        sauce.position.set(-0.2, 0.09, -0.22);
        g.add(sauce);
    }

    function buildGlass(g, color) {
        // Glass body (open cylinder)
        const glassMat = new THREE.MeshStandardMaterial({
            color: color, transparent: true, opacity: 0.28,
            roughness: 0.05, metalness: 0.15, side: THREE.DoubleSide
        });
        const glass = new THREE.Mesh(new THREE.CylinderGeometry(0.22, 0.17, 0.88, 20, 1, true), glassMat);
        g.add(glass);

        // Glass bottom disk
        const bottom = new THREE.Mesh(new THREE.CircleGeometry(0.17, 20), glassMat);
        bottom.rotation.x = Math.PI / 2;
        bottom.position.y = -0.44;
        g.add(bottom);

        // Drink liquid (ayran / lemonade)
        const liquidCol = color === 0x6ABADC ? 0xF5F0E0 : 0xCCEE44; // white for ayran, yellow-green for lemonade
        const liquid = new THREE.Mesh(
            new THREE.CylinderGeometry(0.2, 0.16, 0.7, 20),
            mat(liquidCol, 0.3)
        );
        liquid.position.y = -0.05;
        g.add(liquid);

        // Ice cubes
        const iceMat = mat(0xDDEEFF, 0.1, 0.0);
        for (let i = 0; i < 3; i++) {
            const ice = new THREE.Mesh(new THREE.BoxGeometry(0.09, 0.09, 0.09), iceMat);
            ice.position.set(
                (Math.random() - 0.5) * 0.22,
                0.18 + Math.random() * 0.1,
                (Math.random() - 0.5) * 0.22
            );
            ice.rotation.y = Math.random() * Math.PI;
            g.add(ice);
        }

        // Mint garnish
        const mintMat = mat(0x22CC66, 0.9);
        const mint = new THREE.Mesh(new THREE.PlaneGeometry(0.18, 0.28), mintMat);
        mint.position.set(0.1, 0.42, 0);
        mint.rotation.z = -0.4;
        g.add(mint);

        // Straw
        const straw = new THREE.Mesh(
            new THREE.CylinderGeometry(0.018, 0.018, 1.1, 8),
            mat(0xFF6633, 0.5)
        );
        straw.position.set(0.08, 0.2, 0.08);
        straw.rotation.z = 0.2;
        g.add(straw);
    }

    // ─── Label ───────────────────────────────────────────────────────────────
    function createLabel(text) {
        const canvas = document.createElement('canvas');
        canvas.width  = 320;
        canvas.height = 80;
        const ctx = canvas.getContext('2d');

        ctx.clearRect(0, 0, 320, 80);
        ctx.fillStyle = 'rgba(10,8,4,0.82)';
        roundRect(ctx, 10, 14, 300, 52, 12);
        ctx.fill();

        ctx.strokeStyle = 'rgba(212,168,83,0.7)';
        ctx.lineWidth = 1.5;
        roundRect(ctx, 10, 14, 300, 52, 12);
        ctx.stroke();

        ctx.font = '600 18px Inter, sans-serif';
        ctx.fillStyle = '#D4A853';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(text, 160, 40);

        const texture  = new THREE.CanvasTexture(canvas);
        const material = new THREE.MeshBasicMaterial({ map: texture, transparent: true, depthWrite: false, side: THREE.DoubleSide });
        const plane    = new THREE.Mesh(new THREE.PlaneGeometry(2.2, 0.55), material);
        plane.userData.isLabel = true;
        plane.position.y = 1.5;
        return plane;
    }

    function roundRect(ctx, x, y, w, h, r) {
        ctx.beginPath();
        ctx.moveTo(x + r, y);
        ctx.lineTo(x + w - r, y);
        ctx.arcTo(x + w, y, x + w, y + r, r);
        ctx.lineTo(x + w, y + h - r);
        ctx.arcTo(x + w, y + h, x + w - r, y + h, r);
        ctx.lineTo(x + r, y + h);
        ctx.arcTo(x, y + h, x, y + h - r, r);
        ctx.lineTo(x, y + r);
        ctx.arcTo(x, y, x + r, y, r);
        ctx.closePath();
    }

    // ─── Particles ───────────────────────────────────────────────────────────
    function createFireParticles() {
        const count = 180;
        const positions = new Float32Array(count * 3);
        const colors    = new Float32Array(count * 3);
        const sizes     = new Float32Array(count);

        for (let i = 0; i < count; i++) {
            const r = Math.sqrt(Math.random()) * 3.2;
            const a = Math.random() * Math.PI * 2;
            positions[i*3]   = Math.cos(a) * r;
            positions[i*3+1] = Math.random() * 0.6;
            positions[i*3+2] = Math.sin(a) * r;
            const t = Math.random();
            colors[i*3]   = 1.0;
            colors[i*3+1] = 0.22 + t * 0.45;
            colors[i*3+2] = 0.0;
            sizes[i] = 0.04 + Math.random() * 0.06;
        }

        const geo = new THREE.BufferGeometry();
        geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
        geo.setAttribute('color',    new THREE.BufferAttribute(colors,    3));

        fireParticles = new THREE.Points(geo, new THREE.PointsMaterial({
            size: 0.07, vertexColors: true,
            transparent: true, opacity: 0.85,
            blending: THREE.AdditiveBlending, depthWrite: false,
        }));
        scene.add(fireParticles);
    }

    function createSmokeParticles() {
        const count = 60;
        const positions = new Float32Array(count * 3);
        for (let i = 0; i < count; i++) {
            const a = Math.random() * Math.PI * 2;
            positions[i*3]   = Math.cos(a) * Math.random() * 2;
            positions[i*3+1] = 0.5 + Math.random() * 3;
            positions[i*3+2] = Math.sin(a) * Math.random() * 2;
        }
        const geo = new THREE.BufferGeometry();
        geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
        smokeParticles = new THREE.Points(geo, new THREE.PointsMaterial({
            size: 0.32, color: 0x888888,
            transparent: true, opacity: 0.08,
            depthWrite: false,
        }));
        scene.add(smokeParticles);
    }

    function createEmberParticles() {
        const count = 40;
        const positions = new Float32Array(count * 3);
        for (let i = 0; i < count; i++) {
            const a = Math.random() * Math.PI * 2;
            positions[i*3]   = Math.cos(a) * Math.random() * 4;
            positions[i*3+1] = Math.random() * 5;
            positions[i*3+2] = Math.sin(a) * Math.random() * 4;
        }
        const geo = new THREE.BufferGeometry();
        geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
        emberParticles = new THREE.Points(geo, new THREE.PointsMaterial({
            size: 0.04, color: 0xFF9900,
            transparent: true, opacity: 0.9,
            blending: THREE.AdditiveBlending, depthWrite: false,
        }));
        scene.add(emberParticles);
    }

    // ─── Animate ─────────────────────────────────────────────────────────────
    function animate() {
        animationId = requestAnimationFrame(animate);
        time += 0.012;

        controls.update();

        // Flickering fire lights
        fireLights.forEach(({ light, baseIntensity, offset }) => {
            const flicker = Math.sin(time * 8 + offset) * 0.4 + Math.sin(time * 13 + offset) * 0.2;
            light.intensity = baseIntensity + flicker;
        });

        // Float + gentle rotation on food items, keep labels facing camera
        foodItems.forEach((item, i) => {
            item.position.y = item.userData.baseY + Math.sin(time * 1.4 + i * 0.72) * 0.12;
            item.rotation.y += 0.004;

            for (const child of item.children) {
                if (child.userData.isLabel) {
                    child.quaternion.copy(camera.quaternion);
                }
            }
        });

        // Fire particles rise
        if (fireParticles) {
            const pos = fireParticles.geometry.attributes.position.array;
            for (let i = 0; i < pos.length; i += 3) {
                pos[i+1] += 0.025 + Math.random() * 0.01;
                pos[i]   += (Math.random() - 0.5) * 0.012;
                pos[i+2] += (Math.random() - 0.5) * 0.012;
                if (pos[i+1] > 2.2) {
                    const r = Math.sqrt(Math.random()) * 3.2;
                    const a = Math.random() * Math.PI * 2;
                    pos[i]   = Math.cos(a) * r;
                    pos[i+1] = Math.random() * 0.2;
                    pos[i+2] = Math.sin(a) * r;
                }
            }
            fireParticles.geometry.attributes.position.needsUpdate = true;
        }

        // Smoke drifts upward
        if (smokeParticles) {
            const pos = smokeParticles.geometry.attributes.position.array;
            for (let i = 0; i < pos.length; i += 3) {
                pos[i+1] += 0.012;
                pos[i]   += Math.sin(time + i) * 0.004;
                if (pos[i+1] > 5) pos[i+1] = 0.3;
            }
            smokeParticles.geometry.attributes.position.needsUpdate = true;
            smokeParticles.material.opacity = 0.06 + Math.sin(time * 0.5) * 0.02;
        }

        // Embers float up and drift
        if (emberParticles) {
            const pos = emberParticles.geometry.attributes.position.array;
            for (let i = 0; i < pos.length; i += 3) {
                pos[i+1] += 0.035;
                pos[i]   += (Math.random() - 0.5) * 0.02;
                pos[i+2] += (Math.random() - 0.5) * 0.02;
                if (pos[i+1] > 5.5) {
                    const a = Math.random() * Math.PI * 2;
                    pos[i]   = Math.cos(a) * Math.random() * 3.5;
                    pos[i+1] = Math.random() * 0.4;
                    pos[i+2] = Math.sin(a) * Math.random() * 3.5;
                }
            }
            emberParticles.geometry.attributes.position.needsUpdate = true;
        }

        renderer.render(scene, camera);
    }

    // ─── Interaction ─────────────────────────────────────────────────────────
    function onMouseClick(event) {
        const container = document.getElementById(containerId);
        const rect = container.getBoundingClientRect();
        mouse.x =  ((event.clientX - rect.left) / rect.width)  * 2 - 1;
        mouse.y = -((event.clientY - rect.top)  / rect.height) * 2 + 1;

        raycaster.setFromCamera(mouse, camera);
        const hits = raycaster.intersectObjects(foodItems, true);

        if (hits.length > 0) {
            let obj = hits[0].object;
            while (obj.parent && !obj.userData.category) obj = obj.parent;
            if (obj.userData.category && blazorRef) {
                blazorRef.invokeMethodAsync('OnFoodItemClicked', obj.userData.category);
                // Pulse feedback
                obj.scale.set(1.25, 1.25, 1.25);
                setTimeout(() => obj.scale.set(1, 1, 1), 220);
            }
        }
    }

    function onMouseMove(event) {
        const container = document.getElementById(containerId);
        const rect = container.getBoundingClientRect();
        mouse.x =  ((event.clientX - rect.left) / rect.width)  * 2 - 1;
        mouse.y = -((event.clientY - rect.top)  / rect.height) * 2 + 1;

        raycaster.setFromCamera(mouse, camera);
        const hits = raycaster.intersectObjects(foodItems, true);

        if (hits.length > 0) {
            container.style.cursor = 'pointer';
            let obj = hits[0].object;
            while (obj.parent && !obj.userData.category) obj = obj.parent;
            if (obj !== hoveredItem) {
                if (hoveredItem) hoveredItem.scale.set(1, 1, 1);
                hoveredItem = obj;
                hoveredItem.scale.set(1.15, 1.15, 1.15);
            }
        } else {
            container.style.cursor = 'grab';
            if (hoveredItem) { hoveredItem.scale.set(1, 1, 1); hoveredItem = null; }
        }
    }

    function onResize() {
        const container = document.getElementById(containerId);
        if (!container || !camera) return;
        camera.aspect = container.clientWidth / container.clientHeight;
        camera.updateProjectionMatrix();
        renderer.setSize(container.clientWidth, container.clientHeight);
    }

    function dispose() {
        if (animationId) cancelAnimationFrame(animationId);
        window.removeEventListener('resize', onResize);
        if (renderer) renderer.dispose();
    }

    return { init, dispose };
})();
