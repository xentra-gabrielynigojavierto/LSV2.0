pipeline {
    agent {
        kubernetes {
            yaml '''
apiVersion: v1
kind: Pod
metadata:
  namespace: tools
spec:
  containers:
  - name: docker
    image: docker:24.0.7-dind
    securityContext:
      privileged: true
  - name: aws-k8s-tools
    image: amazon/aws-cli:2.15.15
    command: ["cat"]
    tty: true
'''
        }
    }

    environment {
         AWS_REGION = 'us-east-1' //Make sure this matches the region where your ECR repository and EKS cluster are located
         AWS_ACCOUNT_ID = '637423518666'   //Make sure this matches your AWS account ID
         ECR_REPOSITORY = 'dotnet-core-service' //Make sure this matches the name of your ECR repository
         ECR_REGISTRY = "${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com"
         IMAGE_NAME = "${ECR_REGISTRY}/${ECR_REPOSITORY}"
         EKS_CLUSTER_NAME = 'dev-eks-cluster' //Make sure this matches the name of your EKS cluster
         K8S_NAMESPACE = 'dev' //Make sure this matches the namespace you want to deploy to
         DEPLOYMENT_NAME = 'dotnet-core-deployment' //Make sure this matches the name of your Kubernetes deployment 
         IMAGE_TAG = "${BUILD_NUMBER}-${GIT_COMMIT.take(7)}"
    }

    stages {
        stage('Docker Login') {
            steps {
                container('aws-k8s-tools') {
                    sh "aws ecr get-login-password --region ${AWS_REGION} | docker login --username AWS --password-stdin ${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com"
                }
            }
        }

        stage('Parallel Build & Push') {
            parallel {
                stage('Build Frontends') {
                    steps {
                        container('docker') {
                            // Example for Tenant App
                            sh "docker build --build-arg APP_PORT=5000 -t ${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com/tenant-app:latest ./tenant-app"
                            sh "docker push ${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com/tenant-app:latest"
                        }
                    }
                }
                stage('Build Core Infrastructure') {
                    steps {
                        container('docker') {
                            // Example for Gateway (YARP)
                            sh "docker build --build-arg SERVICE_PORT=5010 -t ${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com/gateway-yarp:latest ./gateway"
                            sh "docker push ${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com/gateway-yarp:latest"
                        }
                    }
                }
                stage('Build Core APIs') {
                    steps {
                        container('docker') {
                            // Example for Identity Service
                            sh "docker build --build-arg SERVICE_PORT=5001 -t ${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com/identity-service:latest ./identity"
                            sh "docker push ${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com/identity-service:latest"
                        }
                    }
                }
            }
        }

        stage('Deploy Cluster Mesh') {
            steps {
                container('aws-k8s-tools') {
                    echo 'Applying updated application manifests to EKS Cluster...'
                    // Loops through your defined architecture directories to patch EKS
                    sh "kubectl apply -f ./k8s/mesh-configuration/"
                }
            }
        }
    }
}