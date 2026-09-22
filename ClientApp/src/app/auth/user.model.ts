export class User {
    constructor(
        public id: number,
        public displayName: string,
        public email: string,
        public profileImageUrl?: string,
        public latitude?: number,
        public longitude?: number,
        public createdAt?: string
    ) {

    }
}
