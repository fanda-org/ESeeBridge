// index.js
document.addEventListener('alpine:init', () => {
    Alpine.data('esee', () => ({
        // Initial data properties
        info: { machineName: '', ipAddress: '', servicePort: 5200, listenerPort: -1 },
        patientId: '',
        result: {},
        isLoading: false,

        // Methods
        async getInfo() {
            try {
                const response = await fetch('api/e-see/info',
                    {
                        method: 'GET',
                        headers: {
                            'Content-Type': 'application/json',
                            'Authorization': 'Basic ZXNlZS1icmlkZ2U6QiF0OWF6aVM='
                        }
                    });
                const result = await response.json();
                this.info = result;
            }
            catch (error) {
                console.error('Error fetching info:', error);
            }
        },

        async getResults() {
            try {
                this.isLoading = true;
                this.result = {};

                const response = await fetch(`api/e-see/send-receive/${this.patientId}`,
                    {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'Authorization': 'Basic ZXNlZS1icmlkZ2U6QiF0OWF6aVM='
                        }
                    });
                this.result = await response.json();
            }
            catch (error) {
                console.error('Error fetching results:', error);
            }
            finally {
                this.isLoading = true;
            }
        },

        // Optional: init() method for initialization logic
        async init() {
            console.log('esee component initialized!');
            // Perform actions when the component is created
            await this.getInfo();
        }
    }));
});