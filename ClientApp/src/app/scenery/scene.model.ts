export class Scene {
    constructor(
        public id: number,
        public title: string,
        public description: string,
        public imageUrl: string,
        public rating: number,
        public ownerUserId?: number,
        public createdAt?: string,
        public updatedAt?: string
    ) {

    }
}