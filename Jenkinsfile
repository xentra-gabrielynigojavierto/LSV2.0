pipeline {
    agent {
        kubernetes {
            yaml '''
apiVersion: v1
kind: Pod
metadata:
  namespace: dev
spec:
  containers:
  - name: docker
    image: docker:24.0.7-dind
    securityContext:
      privileged: true
    tty: true
  - name: aws-k8s-tools
    image: amazon/aws-cli:2.15.15
    command: ["cat"]
    tty: true
'''
        }
    }

    environment {
        AWS_REGION = 'us-east-1'
        AWS_ACCOUNT_ID = '637423518666'
        ECR_REGISTRY = "${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com"
        IMAGE_TAG = "${BUILD_NUMBER}-${GIT_COMMIT.take(7)}"
        EKS_CLUSTER_NAME = 'dev-eks-cluster'
        K8S_NAMESPACE = 'dev'
    }

    stages {

        stage('Docker Login') {
            steps {
                container('docker') {

                    sh '''
                    apk add --no-cache aws-cli
                    aws ecr get-login-password --region $AWS_REGION \
                    | docker login \
                        --username AWS \
                        --password-stdin $ECR_REGISTRY
                    '''
                }
            }
        }

        //=====================================================
    //  STAGE 2: BUILD & PUSH (PARALLEL)
        //=====================================================
        stage('Parallel Build & Push') {

            parallel {


                stage('Tenant App') {
                    steps {
                        container('docker') {
                            sh '''
                            docker build \
                              --build-arg APP_PORT=5000 \
                              -t $ECR_REGISTRY/tenant-app:$IMAGE_TAG \
                             -f ./apps/services/tenant/Dockerfile \
                             .

                            docker push $ECR_REGISTRY/tenant-app:$IMAGE_TAG
                            '''
                        }
                    }
                }

                stage('Gateway') {
                    steps {
                        container('docker') {
                            sh '''
                            docker build \
                              --build-arg APP_PORT=5000 \
                              -t $ECR_REGISTRY/gateway-yarp:$IMAGE_TAG \
                             -f ./apps/gateway/Dockerfile \
                             .

                            docker push $ECR_REGISTRY/gateway-yarp:$IMAGE_TAG
                            '''
                        }
                    }
                }
                stage('Identity Service') {
                    steps {
                        container('docker') {
                            sh '''
                            docker build \
                              --build-arg APP_PORT=5000 \
                              -t $ECR_REGISTRY/identity-service:$IMAGE_TAG \
                             -f ./apps/services/identity/Dockerfile \
                             .

                            docker push $ECR_REGISTRY/identity-service:$IMAGE_TAG
                            '''
                        }
                    }
                }

            }
        }

        stage('Deploy to EKS') {
            steps {
                container('aws-k8s-tools') {

                    sh '''
                    #  Configure kubeconfig for EKS
                    aws eks update-kubeconfig \
                      --region $AWS_REGION \
                      --name $EKS_CLUSTER_NAME

                    #  Apply manifests
                    kubectl apply -f ./k8s/mesh-configuration/
                    '''
                }
            }
        }
    }
}