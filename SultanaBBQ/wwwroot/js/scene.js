// ============================================
// SULTANA BBQ — Interactive 3D Menu Scene
// Three.js scene with stylized food items on a BBQ grill
// ============================================

window.SultanaScene = (() => {
    let scene, camera, renderer, controls;
    let foodItems = [];
    let raycaster, mouse;
    let blazorRef = null;
    let containerId = null;
    let animationId = null;
    let particles = [];
    let time = 0;

    const FOOD_CATEGORIES = [
        { name: 'Grillgerechten', color: 0xC44536, position: { x: 0, y: 1.5, z: 0 }, shape: 'mixed' },
        { name: 'Kebab', color: 0xD4A853, position: { x: -2.5, y: 1.2, z: 1 }, shape: 'cylinder' },
        { name: 'Falafel', color: 0x8B9A46, position: { x: 2.5, y: 1.0, z: 1 }, shape: 'sphere' },
        { name: 'Hamburgers', color: 0x8B4513, position: { x: -1.5, y: 1.3, z: -2 }, shape: 'burger' },
        { name: 'Wraps', color: 0xDEB887, position: { x: 1.5, y: 1.1, z: -2 }, shape: 'wrap' },
        { name: 'Koude Voorgerechten', color: 0x6B8E23, position: { x: -3.5, y: 0.8, z: -1 }, shape: 'bowl' },
        { name: 'Snacks', color: 0xDAA520, position: { x: 3.5, y: 0.9, z: -1 }, shape: 'fries' },
        { name: 'Warme Voorgerechten', color: 0xCD853F, position: { x: 0, y: 1.0, z: 3 }, shape: 'plate' },
        { name: 'Dranken', color: 0x87CEEB, position: { x: 3.8, y: 1.2, z: 2 }, shape: 'glass' },
    ];

    function init(containerElementId, dotNetRef) {
        containerId = containerElementId;
        blazorRef = dotNetRef;

        const container = document.getElementById(containerId);
        if (!container) return;

        // Scene setup
        scene = new THREE.Scene();
        scene.fog = new THREE.FogExp2(0x0D0D0D, 0.08);

        // Camera
        camera = new THREE.PerspectiveCamera(45, container.clientWidth / container.clientHeight, 0.1, 100);
        camera.position.set(0, 5, 10);
        camera.lookAt(0, 0, 0);

        // Renderer
        renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
        renderer.setSize(container.clientWidth, container.clientHeight);
        renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
        renderer.setClearColor(0x0D0D0D, 1);
        renderer.shadowMap.enabled = true;
        renderer.shadowMap.type = THREE.PCFSoftShadowMap;
        container.appendChild(renderer.domElement);

        // Controls
        controls = new THREE.OrbitControls(camera, renderer.domElement);
        controls.enableDamping = true;
        controls.dampingFactor = 0.05;
        controls.maxPolarAngle = Math.PI / 2.2;
        controls.minPolarAngle = Math.PI / 6;
        controls.maxDistance = 15;
        controls.minDistance = 5;
        controls.enablePan = false;
        controls.autoRotate = true;
        controls.autoRotateSpeed = 0.5;

        // Raycaster
        raycaster = new THREE.Raycaster();
        mouse = new THREE.Vector2();

        // Lighting
        setupLighting();

        // Create scene objects
        createGrill();
        createFoodItems();
        createFireParticles();
        createAmbientParticles();

        // Events
        container.addEventListener('click', onMouseClick);
        container.addEventListener('mousemove', onMouseMove);
        window.addEventListener('resize', onResize);

        // Start render loop
        animate();
    }

    function setupLighting() {
        // Ambient light
        const ambient = new THREE.AmbientLight(0x333333, 0.5);
        scene.add(ambient);

        // Main warm light from above
        const mainLight = new THREE.DirectionalLight(0xFFDDBB, 1.0);
        mainLight.position.set(2, 8, 4);
        mainLight.castShadow = true;
        mainLight.shadow.mapSize.width = 1024;
        mainLight.shadow.mapSize.height = 1024;
        scene.add(mainLight);

        // Fire glow from below (grill)
        const fireLight = new THREE.PointLight(0xFF4500, 2, 8);
        fireLight.position.set(0, 0.3, 0);
        scene.add(fireLight);

        // Secondary fire light
        const fireLight2 = new THREE.PointLight(0xD4A853, 1.5, 6);
        fireLight2.position.set(-1, 0.5, 1);
        scene.add(fireLight2);

        // Rim light
        const rimLight = new THREE.PointLight(0xC44536, 0.8, 10);
        rimLight.position.set(-4, 3, -4);
        scene.add(rimLight);

        // Gold accent light
        const goldLight = new THREE.PointLight(0xD4A853, 0.6, 8);
        goldLight.position.set(4, 2, 3);
        scene.add(goldLight);
    }

    function createGrill() {
        // Grill base — circular platform
        const grillGeometry = new THREE.CylinderGeometry(4, 4.2, 0.3, 32);
        const grillMaterial = new THREE.MeshStandardMaterial({
            color: 0x2A2A2A,
            metalness: 0.8,
            roughness: 0.3,
        });
        const grill = new THREE.Mesh(grillGeometry, grillMaterial);
        grill.position.y = 0;
        grill.receiveShadow = true;
        scene.add(grill);

        // Grill grate lines
        const grateMaterial = new THREE.MeshStandardMaterial({
            color: 0x3A3A3A,
            metalness: 0.9,
            roughness: 0.2,
        });

        for (let i = -3; i <= 3; i += 0.5) {
            const bar = new THREE.Mesh(
                new THREE.BoxGeometry(7, 0.05, 0.08),
                grateMaterial
            );
            bar.position.set(0, 0.2, i);
            bar.receiveShadow = true;
            scene.add(bar);
        }

        // Grill rim
        const rimGeometry = new THREE.TorusGeometry(4.1, 0.1, 8, 64);
        const rimMaterial = new THREE.MeshStandardMaterial({
            color: 0xD4A853,
            metalness: 0.9,
            roughness: 0.1,
        });
        const rim = new THREE.Mesh(rimGeometry, rimMaterial);
        rim.rotation.x = Math.PI / 2;
        rim.position.y = 0.2;
        scene.add(rim);

        // Ground plane
        const groundGeometry = new THREE.PlaneGeometry(50, 50);
        const groundMaterial = new THREE.MeshStandardMaterial({
            color: 0x0A0A0A,
            roughness: 1.0,
        });
        const ground = new THREE.Mesh(groundGeometry, groundMaterial);
        ground.rotation.x = -Math.PI / 2;
        ground.position.y = -0.5;
        ground.receiveShadow = true;
        scene.add(ground);
    }

    function createFoodItems() {
        FOOD_CATEGORIES.forEach((item, index) => {
            let mesh;

            switch (item.shape) {
                case 'mixed':
                    mesh = createMixedGrill(item.color);
                    break;
                case 'cylinder':
                    mesh = createKebab(item.color);
                    break;
                case 'sphere':
                    mesh = createFalafel(item.color);
                    break;
                case 'burger':
                    mesh = createBurger(item.color);
                    break;
                case 'wrap':
                    mesh = createWrap(item.color);
                    break;
                case 'bowl':
                    mesh = createBowl(item.color);
                    break;
                case 'fries':
                    mesh = createFries(item.color);
                    break;
                case 'plate':
                    mesh = createPlate(item.color);
                    break;
                case 'glass':
                    mesh = createGlass(item.color);
                    break;
                default:
                    mesh = createDefaultItem(item.color);
            }

            mesh.position.set(item.position.x, item.position.y, item.position.z);
            mesh.userData = { category: item.name, index: index };
            mesh.castShadow = true;

            // Add glow ring beneath each item
            const glowRing = new THREE.Mesh(
                new THREE.RingGeometry(0.4, 0.6, 32),
                new THREE.MeshBasicMaterial({ color: item.color, transparent: true, opacity: 0.3, side: THREE.DoubleSide })
            );
            glowRing.rotation.x = -Math.PI / 2;
            glowRing.position.y = -0.3;
            mesh.add(glowRing);

            // Add floating label
            const label = createLabel(item.name);
            label.position.y = 1.2;
            mesh.add(label);

            scene.add(mesh);
            foodItems.push(mesh);
        });
    }

    function createMixedGrill(color) {
        const group = new THREE.Group();
        // Multiple skewers
        for (let i = -0.5; i <= 0.5; i += 0.5) {
            const skewer = new THREE.Mesh(
                new THREE.CylinderGeometry(0.03, 0.03, 2, 8),
                new THREE.MeshStandardMaterial({ color: 0x8B4513, metalness: 0.3, roughness: 0.7 })
            );
            skewer.rotation.z = Math.PI / 2;
            skewer.position.set(0, 0, i);
            group.add(skewer);

            // Meat pieces on skewer
            for (let j = -0.6; j <= 0.6; j += 0.4) {
                const meat = new THREE.Mesh(
                    new THREE.BoxGeometry(0.25, 0.2, 0.25),
                    new THREE.MeshStandardMaterial({ color: color, roughness: 0.6 })
                );
                meat.position.set(j, 0, i);
                meat.rotation.y = Math.random() * 0.3;
                group.add(meat);
            }
        }
        return group;
    }

    function createKebab(color) {
        const group = new THREE.Group();
        // Vertical döner pillar
        const pillar = new THREE.Mesh(
            new THREE.CylinderGeometry(0.3, 0.4, 1.5, 12),
            new THREE.MeshStandardMaterial({ color: color, roughness: 0.5 })
        );
        group.add(pillar);
        // Top cap
        const cap = new THREE.Mesh(
            new THREE.SphereGeometry(0.3, 12, 8, 0, Math.PI * 2, 0, Math.PI / 2),
            new THREE.MeshStandardMaterial({ color: 0xCC8833, roughness: 0.4 })
        );
        cap.position.y = 0.75;
        group.add(cap);
        return group;
    }

    function createFalafel(color) {
        const group = new THREE.Group();
        // Multiple falafel balls
        const positions = [
            [0, 0, 0], [-0.3, 0, 0.3], [0.3, 0, 0.3],
            [0, 0, -0.3], [0.15, 0.3, 0.1]
        ];
        positions.forEach(pos => {
            const ball = new THREE.Mesh(
                new THREE.SphereGeometry(0.2, 16, 16),
                new THREE.MeshStandardMaterial({ color: color, roughness: 0.7 })
            );
            ball.position.set(...pos);
            group.add(ball);
        });
        return group;
    }

    function createBurger(color) {
        const group = new THREE.Group();
        // Bottom bun
        const bunBottom = new THREE.Mesh(
            new THREE.CylinderGeometry(0.4, 0.45, 0.15, 16),
            new THREE.MeshStandardMaterial({ color: 0xDEB887, roughness: 0.8 })
        );
        bunBottom.position.y = -0.2;
        group.add(bunBottom);
        // Patty
        const patty = new THREE.Mesh(
            new THREE.CylinderGeometry(0.42, 0.42, 0.12, 16),
            new THREE.MeshStandardMaterial({ color: color, roughness: 0.6 })
        );
        patty.position.y = 0;
        group.add(patty);
        // Lettuce
        const lettuce = new THREE.Mesh(
            new THREE.CylinderGeometry(0.44, 0.4, 0.05, 16),
            new THREE.MeshStandardMaterial({ color: 0x4CAF50, roughness: 0.9 })
        );
        lettuce.position.y = 0.1;
        group.add(lettuce);
        // Top bun
        const bunTop = new THREE.Mesh(
            new THREE.SphereGeometry(0.4, 16, 16, 0, Math.PI * 2, 0, Math.PI / 2),
            new THREE.MeshStandardMaterial({ color: 0xDEB887, roughness: 0.8 })
        );
        bunTop.position.y = 0.2;
        group.add(bunTop);
        return group;
    }

    function createWrap(color) {
        const group = new THREE.Group();
        // Cone shape for wrap
        const wrap = new THREE.Mesh(
            new THREE.ConeGeometry(0.35, 1.2, 12),
            new THREE.MeshStandardMaterial({ color: color, roughness: 0.7 })
        );
        wrap.rotation.z = Math.PI / 4;
        group.add(wrap);
        // Filling peeking out
        const filling = new THREE.Mesh(
            new THREE.SphereGeometry(0.3, 12, 12),
            new THREE.MeshStandardMaterial({ color: 0x6B8E23, roughness: 0.8 })
        );
        filling.position.set(0.3, 0.4, 0);
        filling.scale.set(1, 0.6, 1);
        group.add(filling);
        return group;
    }

    function createBowl(color) {
        const group = new THREE.Group();
        // Bowl
        const bowl = new THREE.Mesh(
            new THREE.SphereGeometry(0.45, 16, 16, 0, Math.PI * 2, 0, Math.PI / 2),
            new THREE.MeshStandardMaterial({ color: 0xF5F0E8, roughness: 0.4, metalness: 0.1 })
        );
        bowl.rotation.x = Math.PI;
        group.add(bowl);
        // Contents
        const contents = new THREE.Mesh(
            new THREE.CylinderGeometry(0.4, 0.35, 0.2, 16),
            new THREE.MeshStandardMaterial({ color: color, roughness: 0.8 })
        );
        contents.position.y = 0.1;
        group.add(contents);
        return group;
    }

    function createFries(color) {
        const group = new THREE.Group();
        // Container
        const container = new THREE.Mesh(
            new THREE.BoxGeometry(0.5, 0.6, 0.3),
            new THREE.MeshStandardMaterial({ color: 0xC44536, roughness: 0.5 })
        );
        group.add(container);
        // Fries sticking out
        for (let i = 0; i < 8; i++) {
            const fry = new THREE.Mesh(
                new THREE.BoxGeometry(0.06, 0.5, 0.06),
                new THREE.MeshStandardMaterial({ color: color, roughness: 0.7 })
            );
            fry.position.set(
                (Math.random() - 0.5) * 0.3,
                0.4 + Math.random() * 0.2,
                (Math.random() - 0.5) * 0.2
            );
            fry.rotation.set(
                (Math.random() - 0.5) * 0.3,
                Math.random() * Math.PI,
                (Math.random() - 0.5) * 0.3
            );
            group.add(fry);
        }
        return group;
    }

    function createPlate(color) {
        const group = new THREE.Group();
        // Plate
        const plate = new THREE.Mesh(
            new THREE.CylinderGeometry(0.5, 0.45, 0.08, 24),
            new THREE.MeshStandardMaterial({ color: 0xF5F0E8, roughness: 0.3, metalness: 0.1 })
        );
        group.add(plate);
        // Food items on plate
        const item1 = new THREE.Mesh(
            new THREE.SphereGeometry(0.15, 12, 12),
            new THREE.MeshStandardMaterial({ color: color, roughness: 0.7 })
        );
        item1.position.set(0.15, 0.15, 0);
        item1.scale.y = 0.7;
        group.add(item1);
        const item2 = new THREE.Mesh(
            new THREE.BoxGeometry(0.2, 0.1, 0.3),
            new THREE.MeshStandardMaterial({ color: 0xD4A853, roughness: 0.6 })
        );
        item2.position.set(-0.15, 0.1, 0.1);
        group.add(item2);
        return group;
    }

    function createGlass(color) {
        const group = new THREE.Group();
        // Glass cylinder (transparent)
        const glass = new THREE.Mesh(
            new THREE.CylinderGeometry(0.2, 0.15, 0.8, 16, 1, true),
            new THREE.MeshStandardMaterial({
                color: color, transparent: true, opacity: 0.4,
                roughness: 0.1, metalness: 0.2, side: THREE.DoubleSide
            })
        );
        group.add(glass);
        // Liquid inside
        const liquid = new THREE.Mesh(
            new THREE.CylinderGeometry(0.18, 0.13, 0.6, 16),
            new THREE.MeshStandardMaterial({ color: 0xF5F5DC, roughness: 0.3 })
        );
        liquid.position.y = -0.05;
        group.add(liquid);
        return group;
    }

    function createDefaultItem(color) {
        return new THREE.Mesh(
            new THREE.SphereGeometry(0.4, 16, 16),
            new THREE.MeshStandardMaterial({ color: color, roughness: 0.5 })
        );
    }

    function createLabel(text) {
        const canvas = document.createElement('canvas');
        canvas.width = 256;
        canvas.height = 64;
        const ctx = canvas.getContext('2d');
        ctx.fillStyle = 'rgba(0,0,0,0)';
        ctx.fillRect(0, 0, 256, 64);
        ctx.font = '600 20px Inter, sans-serif';
        ctx.fillStyle = '#D4A853';
        ctx.textAlign = 'center';
        ctx.fillText(text, 128, 38);

        const texture = new THREE.CanvasTexture(canvas);
        const material = new THREE.SpriteMaterial({ map: texture, transparent: true, opacity: 0.9 });
        const sprite = new THREE.Sprite(material);
        sprite.scale.set(2, 0.5, 1);
        return sprite;
    }

    function createFireParticles() {
        const geometry = new THREE.BufferGeometry();
        const count = 100;
        const positions = new Float32Array(count * 3);
        const colors = new Float32Array(count * 3);

        for (let i = 0; i < count; i++) {
            positions[i * 3] = (Math.random() - 0.5) * 6;
            positions[i * 3 + 1] = Math.random() * 0.5;
            positions[i * 3 + 2] = (Math.random() - 0.5) * 6;

            const t = Math.random();
            colors[i * 3] = 1.0;
            colors[i * 3 + 1] = 0.3 + t * 0.4;
            colors[i * 3 + 2] = 0.0;
        }

        geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
        geometry.setAttribute('color', new THREE.BufferAttribute(colors, 3));

        const material = new THREE.PointsMaterial({
            size: 0.05,
            vertexColors: true,
            transparent: true,
            opacity: 0.8,
            blending: THREE.AdditiveBlending,
        });

        const particleSystem = new THREE.Points(geometry, material);
        scene.add(particleSystem);
        particles.push({ mesh: particleSystem, type: 'fire' });
    }

    function createAmbientParticles() {
        const geometry = new THREE.BufferGeometry();
        const count = 50;
        const positions = new Float32Array(count * 3);

        for (let i = 0; i < count; i++) {
            positions[i * 3] = (Math.random() - 0.5) * 20;
            positions[i * 3 + 1] = Math.random() * 8;
            positions[i * 3 + 2] = (Math.random() - 0.5) * 20;
        }

        geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));

        const material = new THREE.PointsMaterial({
            size: 0.03,
            color: 0xD4A853,
            transparent: true,
            opacity: 0.4,
            blending: THREE.AdditiveBlending,
        });

        const particleSystem = new THREE.Points(geometry, material);
        scene.add(particleSystem);
        particles.push({ mesh: particleSystem, type: 'ambient' });
    }

    function animate() {
        animationId = requestAnimationFrame(animate);
        time += 0.01;

        // Update controls
        controls.update();

        // Animate food items (gentle floating)
        foodItems.forEach((item, i) => {
            item.position.y = FOOD_CATEGORIES[i].position.y + Math.sin(time * 1.5 + i * 0.7) * 0.1;
            item.rotation.y += 0.003;
        });

        // Animate fire particles
        particles.forEach(p => {
            if (p.type === 'fire') {
                const positions = p.mesh.geometry.attributes.position.array;
                for (let i = 0; i < positions.length; i += 3) {
                    positions[i + 1] += 0.02;
                    if (positions[i + 1] > 2) {
                        positions[i + 1] = 0;
                        positions[i] = (Math.random() - 0.5) * 6;
                        positions[i + 2] = (Math.random() - 0.5) * 6;
                    }
                }
                p.mesh.geometry.attributes.position.needsUpdate = true;
            } else if (p.type === 'ambient') {
                p.mesh.rotation.y += 0.001;
            }
        });

        renderer.render(scene, camera);
    }

    function onMouseClick(event) {
        const container = document.getElementById(containerId);
        const rect = container.getBoundingClientRect();
        mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
        mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

        raycaster.setFromCamera(mouse, camera);
        const intersects = raycaster.intersectObjects(foodItems, true);

        if (intersects.length > 0) {
            let obj = intersects[0].object;
            // Walk up to find the food item group
            while (obj.parent && !obj.userData.category) {
                obj = obj.parent;
            }
            if (obj.userData.category && blazorRef) {
                blazorRef.invokeMethodAsync('OnFoodItemClicked', obj.userData.category);

                // Visual feedback - pulse animation
                const originalScale = obj.scale.clone();
                obj.scale.multiplyScalar(1.2);
                setTimeout(() => {
                    obj.scale.copy(originalScale);
                }, 200);
            }
        }
    }

    let hoveredItem = null;
    function onMouseMove(event) {
        const container = document.getElementById(containerId);
        const rect = container.getBoundingClientRect();
        mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
        mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

        raycaster.setFromCamera(mouse, camera);
        const intersects = raycaster.intersectObjects(foodItems, true);

        if (intersects.length > 0) {
            container.style.cursor = 'pointer';
            let obj = intersects[0].object;
            while (obj.parent && !obj.userData.category) {
                obj = obj.parent;
            }
            if (obj !== hoveredItem) {
                if (hoveredItem) {
                    // Reset previous
                    hoveredItem.scale.set(1, 1, 1);
                }
                hoveredItem = obj;
                hoveredItem.scale.set(1.1, 1.1, 1.1);
            }
        } else {
            container.style.cursor = 'grab';
            if (hoveredItem) {
                hoveredItem.scale.set(1, 1, 1);
                hoveredItem = null;
            }
        }
    }

    function onResize() {
        const container = document.getElementById(containerId);
        if (!container) return;
        camera.aspect = container.clientWidth / container.clientHeight;
        camera.updateProjectionMatrix();
        renderer.setSize(container.clientWidth, container.clientHeight);
    }

    function dispose() {
        if (animationId) {
            cancelAnimationFrame(animationId);
        }
        window.removeEventListener('resize', onResize);
        if (renderer) {
            renderer.dispose();
        }
    }

    return { init, dispose };
})();
